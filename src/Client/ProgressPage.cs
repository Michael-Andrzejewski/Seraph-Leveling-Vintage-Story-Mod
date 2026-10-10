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
    /// Handbook page "Seraph Leveling: My Progress". The server pushes the
    /// report on join, on level-ups and every 15 s when it changed. On top of
    /// that the page asks for a fresh report the moment it is opened and once
    /// a second while it stays open, and redraws itself when new numbers
    /// arrive, so mining a block shows up on the page within about a second.
    /// </summary>
    public class SeraphProgressPage : GuiHandbookTextPage
    {
        private readonly ICoreClientAPI capi;

        /// <summary>The page the handbook currently holds (recreated with the handbook).</summary>
        public static SeraphProgressPage Instance;

        /// <summary>Mod channel, set by the client system; used to ask the server for a report.</summary>
        public static IClientNetworkChannel Channel;

        /// <summary>Last report received over the network channel.</summary>
        public static string LatestReport;

        /// <summary>The report this page last drew, so an unchanged re-sync does not redraw.</summary>
        private string shownReport;

        private static readonly System.Reflection.FieldInfo browseHistoryField =
            typeof(GuiDialogHandbook).GetField("browseHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo detailViewGuiField =
            typeof(GuiDialogHandbook).GetField("detailViewGui", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public SeraphProgressPage(ICoreClientAPI capi)
        {
            this.capi = capi;
            pageCode = "seraphleveling-progress";
            Title = "sl-progress-title";
            categoryCode = "guide";
            Text = "sl-progress-title";
            Init(capi);
            Instance = this;
        }

        /// <summary>Ask the server for the current report. force = send it even if nothing changed.</summary>
        public static void RequestReport(bool force)
        {
            try { Channel?.SendPacket(new ProgressReportRequestMessage { Force = force }); }
            catch (Exception ex) { Instance?.capi?.Logger?.Debug("[SeraphLeveling] progress report request failed: {0}", ex.Message); }
        }

        /// <summary>Network handler: keep the report and redraw the page if it is on screen.</summary>
        public static void OnReportReceived(string report)
        {
            LatestReport = report;
            Instance?.RefreshIfShowing();
        }

        /// <summary>Client tick: while the handbook shows this page, keep asking for changes.</summary>
        public static void PollIfShowing()
        {
            var page = Instance;
            if (page == null || page.DialogShowingThis() == null) return;
            // Paused single player: nothing can change, and the server would not answer until unpaused.
            if (page.capi.IsSinglePlayer && page.capi.IsGamePaused) return;
            RequestReport(false);
        }

        /// <summary>
        /// Single player runs the server in this process with the same mod
        /// statics, and opening the handbook pauses it, so a request sent now
        /// would only be answered after the page closes. Build the report here
        /// instead. Null when there is no in-process server or the build failed
        /// (the server may still be finishing its last tick; the caller then
        /// keeps the last report and asks over the network as usual).
        /// </summary>
        private string BuildInProcess()
        {
            if (!capi.IsSinglePlayer || capi.OpenedToLan) return null;
            var sapi = SeraphLevelingModSystem.ServerApi;
            string uid = capi.World?.Player?.PlayerUID;
            if (sapi?.World == null || uid == null) return null;
            try
            {
                if (!(sapi.World.PlayerByUid(uid) is IServerPlayer sp) || sp.Entity == null) return null;
                string report = SeraphLevelingModSystem.BuildProgressReport(sp);
                return string.IsNullOrEmpty(report) ? null : report;
            }
            catch (Exception ex)
            {
                capi.Logger.Debug("[SeraphLeveling] in-process progress report failed, asking the server: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>The open handbook dialog with this page on screen, or null.</summary>
        private GuiDialogHandbook DialogShowingThis()
        {
            try
            {
                foreach (object gui in capi.OpenedGuis)
                {
                    if (!(gui is GuiDialogHandbook dlg) || !dlg.IsOpened()) continue;
                    if (browseHistoryField?.GetValue(dlg) is Stack<BrowseHistoryElement> history
                        && history.Count > 0 && history.Peek().Page == this && history.Peek().SearchText == null)
                        return dlg;
                }
            }
            catch (Exception ex) { SeraphLevelingModSystem.LogFailureOnce("finding the open handbook progress page", ex); }
            return null;
        }

        private void RefreshIfShowing()
        {
            if (LatestReport == shownReport) return;
            var dlg = DialogShowingThis();
            if (dlg == null) return;
            if (TryUpdateTextInPlace(dlg)) return;
            dlg.ReloadPage();   // fallback: recomposes the whole dialog; the scroll position lives in the history entry and survives
        }

        /// <summary>
        /// Update the open page without rebuilding it. ReloadPage rebuilds the
        /// whole handbook dialog, and the report changes about once a second
        /// while the page is open (hunger and armour counters tick). The page
        /// text is drawn per report section (SeraphReportRichtext), so only the
        /// sections whose text changed are laid out and drawn again. Returns
        /// false if the layout is not what we expect (or the section count
        /// changed), and the caller falls back to ReloadPage.
        /// </summary>
        private bool TryUpdateTextInPlace(GuiDialogHandbook dlg)
        {
            try
            {
                if (!(detailViewGuiField?.GetValue(dlg) is GuiComposer composer)) return false;
                var richtext = composer.GetRichtext("richtext") as SeraphReportRichtext;
                var scrollbar = composer.GetScrollbar("scrollbar");
                if (richtext == null || scrollbar == null) return false;

                float scrollY = scrollbar.CurrentYPosition;
                if (!richtext.Update(CurrentSections())) return false;
                shownReport = LatestReport;
                scrollbar.SetNewTotalHeight((float)richtext.Bounds.fixedHeight);
                scrollbar.CurrentYPosition = scrollY;   // the handle position is stored relative to the total height
                return true;
            }
            catch (Exception ex)
            {
                capi?.Logger?.Debug("[SeraphLeveling] in-place progress page update failed, reloading the page: {0}", ex.Message);
                return false;
            }
        }

        private string CurrentVtml() => string.Concat(CurrentSections());

        /// <summary>
        /// The page as VTML, one string per report section: the title, then a
        /// new section at every === / --- header line, then the footer.
        /// </summary>
        private List<string> CurrentSections()
        {
            string report = LatestReport;
            var sections = new List<string>();
            var sb = new StringBuilder();
            sb.Append("<strong>").Append(Lang.Get("sl-progress-title")).Append("</strong><br><br>");
            if (string.IsNullOrEmpty(report))
            {
                sb.Append(Lang.Get("sl-progress-waiting"));
            }
            else
            {
                foreach (string raw in report.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) { sb.Append("<br>"); continue; }
                    string safe = line.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    if (line.StartsWith("===", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal))
                    {
                        if (sb.Length > 0) { sections.Add(sb.ToString()); sb.Clear(); }
                        sb.Append("<strong>").Append(safe.Trim('=', '-', ' ')).Append("</strong><br>");
                    }
                    else
                        sb.Append(safe).Append("<br>");
                }
            }
            if (sb.Length > 0) sections.Add(sb.ToString());
            sections.Add("<br>" + Lang.Get("sl-progress-footer"));
            return sections;
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            string local = BuildInProcess();
            if (local != null) LatestReport = local;
            shownReport = LatestReport;
            detailViewGui.AddInteractiveElement(new SeraphReportRichtext(capi, CurrentSections(), PageFont(), textBounds), "richtext");
            // Page just opened (or redrawn): make sure the numbers are current.
            RequestReport(force: shownReport == null);
        }

        private static CairoFont PageFont() => CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

        public override PageText GetPageText()
        {
            return new PageText { Title = Lang.Get("sl-progress-title").ToSearchFriendly(), Text = CurrentVtml() };
        }
    }

    /// <summary>
    /// The progress page text, drawn as one texture per report section instead
    /// of one texture for the whole ~4000 px report. A report update lays out
    /// and draws only the sections whose text changed (laying out and drawing
    /// the whole report took about 40 ms, a visible hitch every second while
    /// the page was open). It is still a GuiElementRichtext keyed "richtext",
    /// so the handbook's own scrolling (which moves this element's bounds; the
    /// section bounds are its children and follow) and scroll range (this
    /// element's fixedHeight) work unchanged.
    /// </summary>
    public class SeraphReportRichtext : GuiElementRichtext
    {
        private readonly CairoFont font;
        private readonly List<GuiElementRichtext> sections = new List<GuiElementRichtext>();
        private readonly List<string> sectionVtml = new List<string>();

        public SeraphReportRichtext(ICoreClientAPI capi, IList<string> vtml, CairoFont font, ElementBounds bounds)
            : base(capi, new RichTextComponentBase[0], bounds)
        {
            this.font = font;
            foreach (string v in vtml)
            {
                var b = ElementBounds.Fixed(0, 0, bounds.fixedWidth, 0);
                bounds.WithChild(b);
                sections.Add(new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, v, font), b));
                sectionVtml.Add(v);
            }
        }

        /// <summary>Stack the sections top to bottom; this element's height is their sum.</summary>
        private void Restack()
        {
            double y = 0;
            foreach (var s in sections)
            {
                s.Bounds.fixedY = y;
                y += s.Bounds.fixedHeight;
            }
            Bounds.fixedHeight = y;
        }

        public override void BeforeCalcBounds()
        {
            foreach (var s in sections) s.CalcHeightAndPositions();
            Restack();
        }

        public override void ComposeElements(Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            Bounds.CalcWorldBounds();
            foreach (var s in sections) s.Compose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            foreach (var s in sections) s.RenderInteractiveElements(deltaTime);
        }

        /// <summary>
        /// Swap in new section texts, redrawing only the changed ones. Returns
        /// false when the number of sections differs (the caller rebuilds).
        /// </summary>
        public bool Update(IList<string> vtml)
        {
            if (vtml.Count != sections.Count) return false;
            bool moved = false;
            for (int i = 0; i < sections.Count; i++)
            {
                if (vtml[i] == sectionVtml[i]) continue;
                double before = sections[i].Bounds.fixedHeight;
                sections[i].SetNewText(VtmlUtil.Richtextify(api, vtml[i], font));
                sectionVtml[i] = vtml[i];
                if (sections[i].Bounds.fixedHeight != before) moved = true;
            }
            if (moved)
            {
                Restack();
                Bounds.CalcWorldBounds();
            }
            return true;
        }

        public override void Dispose()
        {
            foreach (var s in sections) s.Dispose();
            base.Dispose();
        }
    }
}
