using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling
{
    /// <summary>
    /// Client-side mod system that displays mining progression in the character traits dialog.
    /// Uses Harmony to patch the CharacterSystem's trait display method and adds scrollable traits UI.
    /// </summary>
    public class SeraphLevelingClientSystem : ModSystem
    {
        private ICoreClientAPI clientApi;
        private Harmony harmony;

        // Scroll-related fields for traits UI
        private GuiDialogCharacterBase charDlg;

        /// <summary>The vanilla trait tab handler we swapped out, kept so Dispose can put it back.</summary>
        private Action<GuiComposer> replacedVanillaTraitsHandler;

        /// <summary>How many times TryHookCharacterDialog has looked for the vanilla handler and not found it.</summary>
        private int hookAttempts = 0;
        private ElementBounds clippingBounds;
        private ElementBounds scrollbarBounds;
        private GuiElementRichtext richtextElem;
        private bool hasHookedDialog = false;
        private object characterSystemInstance;
        private long progressPollListenerId;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            clientApi = api;

            // Mirror server's IsCombatOverhaulLoaded flag on the client. The server-side
            // assignment in StartServerSide doesn't run on a client-only instance, so without
            // this the postfix would think CO is never loaded and skip CO trait display
            // even when CO is actually installed. Accepts the original mod and the 1.22 fork.
            SeraphLevelingModSystem.IsCombatOverhaulLoaded =
                SeraphLevelingModSystem.DetectAnyCombatOverhaul(api.ModLoader);
            SeraphLevelingModSystem.IsCombatOverhaulForkLoaded = api.ModLoader.IsModEnabled("combatoverhaulfork");

            // Register network channel for receiving level-up sounds and the
            // experimental-feature config sync from the server
            SeraphProgressPage.Channel = api.Network.RegisterChannel("seraphleveling")
                .RegisterMessageType<LevelUpSoundMessage>()
                .RegisterMessageType<ExperimentalFeatureConfigMessage>()
                .RegisterMessageType<ProgressReportMessage>()
                .RegisterMessageType<ProgressReportRequestMessage>()
                .SetMessageHandler<LevelUpSoundMessage>(OnLevelUpSoundReceived)
                .SetMessageHandler<ExperimentalFeatureConfigMessage>(SeraphLevelingModSystem.ApplyFeatureConfigMessage)
                .SetMessageHandler<ProgressReportMessage>(msg =>
                {
                    if (SeraphLevelingModSystem.DebugLoggingEnabled)
                        api.Logger.Debug("[SeraphLeveling] progress report received ({0} chars)", msg?.Report?.Length ?? 0);
                    SeraphProgressPage.OnReportReceived(msg?.Report);
                });

            // While the progress page is on screen, ask the server for changes once a second.
            progressPollListenerId = api.Event.RegisterGameTickListener(_ => SeraphProgressPage.PollIfShowing(), 1000);

            // Apply Harmony patches manually for better control
            harmony = new Harmony("seraphleveling");
            try
            {
                ApplyPatches(api);
                api.Logger.Notification("[SeraphLeveling] Client-side mod loaded, Harmony patches applied");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply Harmony patches: {ex.Message}");
                api.Logger.Error($"[SeraphLeveling] Stack trace: {ex.StackTrace}");
            }

            // Register event to hook into character dialog when it's loaded
            api.Event.PlayerJoin += OnPlayerJoin;

            // Live progression page in the handbook (Guides section).
            var handbook = api.ModLoader.GetModSystem<ModSystemSurvivalHandbook>();
            if (handbook != null)
            {
                handbook.OnInitCustomPages += pages => pages.Add(new SeraphProgressPage(api));
            }
        }

        /// <summary>
        /// Called when the server sends a level-up sound message. Plays the sound locally on the client.
        /// </summary>
        private void OnLevelUpSoundReceived(LevelUpSoundMessage message)
        {
            try
            {
                var player = clientApi?.World?.Player?.Entity;
                if (player != null && !string.IsNullOrEmpty(message?.SoundName))
                {
                    float clamped = Math.Clamp(message.Volume, 0f, 1f);
                    clientApi.World.PlaySoundAt(new AssetLocation(message.SoundName), player, null, true, 16f, clamped);

                    if (message.IsTest)
                    {
                        string rawVolStr = message.Volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        string playedVolStr = clamped.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        string label = $"[SeraphLeveling] Test sound received. sound={message.SoundName}, volume sent={rawVolStr}, volume played={playedVolStr}";
                        clientApi.Logger.Notification(label);
                        clientApi.ShowChatMessage(label);
                    }
                }
            }
            catch (Exception ex)
            {
                clientApi?.Logger.Warning($"[SeraphLeveling] Failed to play level-up sound: {ex.Message}");
            }
        }

        private void OnPlayerJoin(IClientPlayer byPlayer)
        {
            // Try to hook into the character dialog after a short delay to ensure it's loaded
            clientApi.Event.RegisterCallback(TryHookCharacterDialog, 500);
        }

        private void TryHookCharacterDialog(float dt)
        {
            if (hasHookedDialog) return;

            try
            {
                // Find the character dialog in loaded GUIs
                charDlg = clientApi.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;

                if (charDlg == null)
                {
                    // Dialog not loaded yet, try again later. Count the attempt so a
                    // dialog that never appears cannot keep the retry loop alive for
                    // the whole session.
                    hookAttempts++;
                    if (hookAttempts < 20)
                    {
                        clientApi.Event.RegisterCallback(TryHookCharacterDialog, 1000);
                    }
                    return;
                }

                // Find the CharacterSystem instance to call getClassTraitText
                var characterSystemType = AccessTools.TypeByName("Vintagestory.GameContent.CharacterSystem");
                if (characterSystemType != null)
                {
                    // Get the mod system using the type's full name
                    characterSystemInstance = clientApi.ModLoader.GetModSystem(characterSystemType.FullName);
                }

                // Find the vanilla CharacterSystem's trait tab handler. Each tab in the
                // dialog renders RenderTabHandlers[tab.DataInt], so the handler list and
                // the tab list must keep the same positions. Other mods (the Combat
                // Overhaul fork's armor tab among them) append their own handlers, so we
                // must never remove or insert entries: that shifts every later handler
                // one position and their tabs start rendering the wrong content. Replace
                // the vanilla handler IN PLACE instead; positions cannot move.
                int handlerIndex = -1;
                for (int i = 0; i < charDlg.RenderTabHandlers.Count; i++)
                {
                    var handler = charDlg.RenderTabHandlers[i];
                    if (handler?.Target == null) continue;
                    // The vanilla handler is an instance method on the CharacterSystem mod
                    // system. Match the instance we already looked up; fall back to the
                    // type name for unexpected builds.
                    if (ReferenceEquals(handler.Target, characterSystemInstance) ||
                        handler.Target.GetType().FullName?.Contains("CharacterSystem") == true)
                    {
                        handlerIndex = i;
                        break;
                    }
                }

                if (handlerIndex < 0)
                {
                    // The vanilla handler is not registered yet (mod load order and timing
                    // vary). Retry for a while, then leave the dialog alone: the vanilla
                    // trait tab keeps working, only our scrollbar is lost. Never insert at
                    // a guessed position; a wrong guess breaks every mod-added tab.
                    hookAttempts++;
                    if (hookAttempts < 20)
                    {
                        clientApi.Event.RegisterCallback(TryHookCharacterDialog, 1000);
                    }
                    else
                    {
                        clientApi.Logger.Warning("[SeraphLeveling] Gave up hooking the character dialog trait tab: vanilla handler never appeared. Trait tab stays vanilla (not scrollable).");
                    }
                    return;
                }

                replacedVanillaTraitsHandler = charDlg.RenderTabHandlers[handlerIndex];
                charDlg.RenderTabHandlers[handlerIndex] = ComposeTraitsTab;
                clientApi.Logger.Debug("[SeraphLeveling] Replaced vanilla trait tab handler in place at index {0}", handlerIndex);
                hasHookedDialog = true;

                clientApi.Logger.Notification("[SeraphLeveling] Successfully hooked into character dialog for scrollable traits");
            }
            catch (Exception ex)
            {
                clientApi.Logger.Error($"[SeraphLeveling] Failed to hook character dialog: {ex.Message}");
                // Retry after a delay, under the same attempt cap as the other branches.
                hookAttempts++;
                if (hookAttempts < 20)
                {
                    clientApi.Event.RegisterCallback(TryHookCharacterDialog, 2000);
                }
            }
        }

        /// <summary>
        /// Composes the traits tab with scrolling support.
        /// </summary>
        private void ComposeTraitsTab(GuiComposer compo)
        {
            // Get the trait text from the CharacterSystem (our postfix patch will modify it)
            string traitText = GetClassTraitText();

            // Define bounds for the scrollable area
            // Standard traits tab area is approximately 385x310 pixels
            clippingBounds = ElementBounds.Fixed(0, 25, 385, 310);

            // Begin clip area for scrollable content
            compo.BeginClip(clippingBounds);

            // Add richtext element for trait display
            // Use a tall container to hold all traits (will be clipped and scrolled)
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 370, 1000);
            compo.AddRichtext(traitText, CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), textBounds, "traitsText");

            compo.EndClip();

            // Add scrollbar to the right of the clipping area
            scrollbarBounds = clippingBounds.RightCopy().WithFixedWidth(10).WithFixedPadding(3, 0);
            compo.AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "traitsScrollbar");

            // Get reference to the richtext element for scroll updates
            richtextElem = compo.GetRichtext("traitsText");

            // Calculate and set scroll heights after composition
            // We need to set this after the composer has composed to get accurate heights
            compo.OnComposed += () =>
            {
                SetScrollbarHeights(compo);
            };
        }

        /// <summary>
        /// Sets the scrollbar heights based on actual content size.
        /// </summary>
        private void SetScrollbarHeights(GuiComposer compo)
        {
            try
            {
                var scrollbar = compo.GetScrollbar("traitsScrollbar");
                var richtext = compo.GetRichtext("traitsText");

                if (scrollbar != null && richtext != null)
                {
                    float visibleHeight = (float)clippingBounds.fixedHeight;
                    // Get actual content height from richtext bounds
                    float totalHeight = (float)richtext.Bounds.fixedHeight;

                    // If content fits, use visible height as total (no scrolling needed)
                    if (totalHeight < visibleHeight)
                    {
                        totalHeight = visibleHeight;
                    }

                    scrollbar.SetHeights(visibleHeight, totalHeight);
                }
            }
            catch (Exception ex)
            {
                clientApi?.Logger?.Debug($"[SeraphLeveling] Error setting scrollbar heights: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback when scrollbar value changes - adjusts content position.
        /// </summary>
        private void OnNewScrollbarValue(float value)
        {
            if (richtextElem != null)
            {
                richtextElem.Bounds.fixedY = 0 - value;
                richtextElem.Bounds.CalcWorldBounds();
            }
        }

        /// <summary>
        /// Gets the trait text by calling the CharacterSystem's getClassTraitText method.
        /// Our postfix patch will modify this text to include dynamic trait values.
        /// </summary>
        private string GetClassTraitText()
        {
            try
            {
                if (characterSystemInstance != null)
                {
                    var method = AccessTools.Method(characterSystemInstance.GetType(), "getClassTraitText");
                    if (method != null)
                    {
                        return method.Invoke(characterSystemInstance, null) as string ?? "";
                    }
                }

                // Fallback: return empty if we can't get the trait text
                return Lang.Get("charactersheet-notraits");
            }
            catch (Exception ex)
            {
                clientApi?.Logger?.Debug($"[SeraphLeveling] Error getting trait text: {ex.Message}");
                return "";
            }
        }

        private void ApplyPatches(ICoreClientAPI api)
        {
            // Set the API reference for the patch to use
            CharacterSystemPatches.ClientApi = api;

            // Experimental (config-gated, off by default): mirror the server-side bow
            // draw-speed patch so draw prediction matches, converge the aim reticle by
            // ranged level, and speed melee swings by melee level. Applied before the
            // trait-text patch so they land even if that one bails.
            BowDrawSpeedPatches.ApplyClient(harmony, api);
            AimReticlePatches.Apply(harmony, api);
            MeleeAttackSpeedPatches.Apply(harmony, api, "client");

            // Find the CharacterSystem type
            var characterSystemType = AccessTools.TypeByName("Vintagestory.GameContent.CharacterSystem");
            if (characterSystemType == null)
            {
                api.Logger.Warning("[SeraphLeveling] Could not find CharacterSystem type");
                return;
            }

            // Find the getClassTraitText method
            var targetMethod = AccessTools.Method(characterSystemType, "getClassTraitText");
            if (targetMethod == null)
            {
                api.Logger.Warning("[SeraphLeveling] Could not find getClassTraitText method");

                // List available methods for debugging
                var methods = characterSystemType.GetMethods(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                api.Logger.Debug($"[SeraphLeveling] Available methods in CharacterSystem:");
                foreach (var m in methods)
                {
                    if (m.Name.ToLower().Contains("trait"))
                    {
                        api.Logger.Debug($"  - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
                    }
                }
                return;
            }

            api.Logger.Debug($"[SeraphLeveling] Found method: {targetMethod.Name}, params: {string.Join(", ", targetMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");

            // Get our postfix method
            var postfixMethod = AccessTools.Method(typeof(CharacterSystemPatches), nameof(CharacterSystemPatches.GetClassTraitText_Postfix));

            // Apply the patch
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            api.Logger.Notification("[SeraphLeveling] Successfully patched getClassTraitText");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("seraphleveling");
            SeraphProgressPage.LatestReport = null;   // never show the last world's numbers in the next one

            // See the server Dispose: the guards must reset with the unpatch.
            BowDrawSpeedPatches.PatchedInProcess = false;
            SeraphLevelingModSystem.ResetCharacterSystemCache();
            MeleeAttackSpeedPatches.ResetPatchGuard();

            // Unhook from character dialog: put the vanilla handler back in the same
            // slot. Removing our entry instead would shift every later handler and
            // misalign other mods' tabs (see TryHookCharacterDialog).
            if (charDlg != null && hasHookedDialog)
            {
                try
                {
                    int idx = charDlg.RenderTabHandlers.IndexOf(ComposeTraitsTab);
                    if (idx >= 0 && replacedVanillaTraitsHandler != null)
                    {
                        charDlg.RenderTabHandlers[idx] = replacedVanillaTraitsHandler;
                    }
                    else if (idx >= 0)
                    {
                        charDlg.RenderTabHandlers.RemoveAt(idx);
                    }
                }
                catch (Exception ex) { SeraphLevelingModSystem.LogFailureOnce("removing the traits tab from the character dialog", ex, clientSide: true); }
            }

            if (clientApi != null)
            {
                clientApi.Event.PlayerJoin -= OnPlayerJoin;
                if (progressPollListenerId != 0)
                {
                    try { clientApi.Event.UnregisterGameTickListener(progressPollListenerId); } catch (Exception ex) { SeraphLevelingModSystem.LogFailureOnce("unregistering the progress page poll", ex, clientSide: true); }
                    progressPollListenerId = 0;
                }
            }
            SeraphProgressPage.Channel = null;
            SeraphProgressPage.Instance = null;

            base.Dispose();
        }
    }
}
