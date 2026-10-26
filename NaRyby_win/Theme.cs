using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NaRyby_win
{
    /// <summary>
    /// Motyw jasny albo ciemny – wybrany w programie albo zgodny z ustawieniem Windows („Tryb aplikacji”).
    /// Kolory są te same co w wersji na macOS. Po zmianie motywu otwarte okna przemalowują się same.
    /// </summary>
    internal static class Theme
    {
        // Role kontrolek, których kolor nie wynika z rodzaju kontrolki (zapisywane w Tag).
        private const string DimRole = "theme:dim";
        private const string AccentRole = "theme:accent";
        private const string SurfaceRole = "theme:surface";

        public static bool IsDark { get; private set; }

        private static string _mode;

        /// <summary>Wybrany motyw: "light", "dark" albo null – zgodnie z systemem.</summary>
        public static string Mode
        {
            get { return _mode; }
            set
            {
                _mode = value;
                Update();
            }
        }

        /// <summary>Wywoływane po przełączeniu motywu – dla elementów kolorowanych poza <see cref="Apply"/>.</summary>
        public static event EventHandler Changed;

        public static Color WindowBackground { get { return Pick(Color.FromArgb(245, 247, 250), Color.FromArgb(30, 32, 36)); } }
        public static Color Surface { get { return Pick(Color.White, Color.FromArgb(42, 45, 50)); } }
        public static Color Border { get { return Pick(Color.FromArgb(208, 214, 222), Color.FromArgb(68, 73, 80)); } }
        public static Color HeaderBackground { get { return Pick(Color.FromArgb(230, 235, 242), Color.FromArgb(54, 59, 67)); } }
        public static Color Selection { get { return Pick(Color.FromArgb(200, 220, 245), Color.FromArgb(48, 78, 112)); } }
        public static Color Text { get { return Pick(Color.Black, Color.FromArgb(232, 232, 232)); } }
        public static Color DimText { get { return Pick(Color.DimGray, Color.FromArgb(160, 164, 170)); } }
        public static Color Accent { get { return Pick(Color.SteelBlue, Color.FromArgb(120, 175, 225)); } }
        public static Color Gold { get { return Pick(Color.DarkGoldenrod, Color.FromArgb(230, 180, 60)); } }
        public static Color Link { get { return Pick(Color.FromArgb(0, 102, 204), Color.FromArgb(110, 170, 255)); } }

        public static Color ChartNight { get { return Pick(Color.FromArgb(232, 236, 246), Color.FromArgb(46, 52, 68)); } }
        public static Color ChartBest { get { return Pick(Color.FromArgb(215, 240, 215), Color.FromArgb(40, 72, 46)); } }
        public static Color ChartGrid { get { return Pick(Color.FromArgb(225, 225, 225), Color.FromArgb(62, 66, 72)); } }
        public static Color ChartPressure { get { return Pick(Color.FromArgb(40, 90, 200), Color.FromArgb(110, 160, 255)); } }
        public static Color ChartLegendBorder { get { return Pick(Color.Silver, Color.FromArgb(100, 104, 110)); } }

        public static Color ClosedRow { get { return Pick(Color.FromArgb(253, 226, 226), Color.FromArgb(92, 40, 40)); } }
        public static Color SoonRow { get { return Pick(Color.FromArgb(255, 243, 205), Color.FromArgb(84, 70, 30)); } }

        private static Color Pick(Color light, Color dark)
        {
            return IsDark ? dark : light;
        }

        /// <summary>Delikatne tło w kolorze oceny (wyróżnienie najlepszego dnia) – rozjaśnione albo przyciemnione do tła.</summary>
        public static Color SoftScoreColor(int score)
        {
            Color c = FishingAdvisor.ScoreColor(score), back = Surface;
            return Color.FromArgb(back.R + (c.R - back.R) / 3, back.G + (c.G - back.G) / 3, back.B + (c.B - back.B) / 3);
        }

        /// <summary>Oznacza okno, które ma tło jak panel (białe / grafitowe) zamiast tła okna.</summary>
        public static void UseSurface(Form form)
        {
            form.Tag = SurfaceRole;
        }

        /// <summary>Oznacza kontrolkę jako tekst drugoplanowy (szary).</summary>
        public static T Dim<T>(T control) where T : Control
        {
            control.Tag = DimRole;
            control.ForeColor = DimText;
            return control;
        }

        /// <summary>Oznacza kontrolkę jako tekst w kolorze akcentu (niebieski).</summary>
        public static T Accented<T>(T control) where T : Control
        {
            control.Tag = AccentRole;
            control.ForeColor = Accent;
            return control;
        }

        #region Wykrywanie i zmiana motywu

        static Theme()
        {
            IsDark = SystemPrefersDark();
            SystemEvents.UserPreferenceChanged += (s, e) => Update();
        }

        private static void Update()
        {
            bool dark = _mode == "dark" || (_mode != "light" && SystemPrefersDark());
            if (dark == IsDark) return;
            IsDark = dark;
            foreach (Form form in Application.OpenForms) Apply(form);
            var handler = Changed;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        private static bool SystemPrefersDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Kolorowanie kontrolek

        /// <summary>Koloruje okno i wszystkie jego kontrolki według bieżącego motywu.</summary>
        public static void Apply(Form form)
        {
            if (form.IsHandleCreated) SetDarkTitleBar(form);
            else form.HandleCreated += (s, e) => SetDarkTitleBar(form);

            form.BackColor = (form.Tag as string) == SurfaceRole ? Surface : WindowBackground;
            form.ForeColor = Text;
            ApplyChildren(form);
        }

        private static void ApplyChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                ApplyControl(c);
                ApplyChildren(c);
            }
        }

        private static void ApplyControl(Control c)
        {
            if (c is ScoreChart)
            {
                c.BackColor = Surface;
                c.Invalidate();
            }
            else if (c is DataGridView) ApplyGrid((DataGridView)c);
            else if (c is TextBox || c is ListBox)
            {
                c.BackColor = Surface;
                c.ForeColor = Text;
                SetControlTheme(c, "DarkMode_Explorer");
            }
            else if (c is ComboBox)
            {
                var combo = (ComboBox)c;
                combo.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                combo.BackColor = IsDark ? Surface : SystemColors.Window;
                combo.ForeColor = Text;
                SetControlTheme(c, "DarkMode_CFD");
            }
            else if (c is Button) ApplyButton((Button)c);
            else if (c is TabControl)
            {
                var tabs = c as ThemedTabControl;
                if (tabs != null) tabs.Invalidate();
                foreach (TabPage page in ((TabControl)c).TabPages)
                {
                    page.BackColor = Surface;
                    page.ForeColor = Text;
                }
            }
            else if (c is StatusStrip)
            {
                var strip = (ToolStrip)c;
                strip.Renderer = IsDark ? (ToolStripRenderer)new DarkToolStripRenderer() : new ToolStripProfessionalRenderer();
                strip.BackColor = IsDark ? HeaderBackground : SystemColors.Control;
                strip.ForeColor = Text;
            }
            else if (c is LinkLabel)
            {
                var link = (LinkLabel)c;
                link.LinkColor = Link;
                link.ActiveLinkColor = Link;
                link.VisitedLinkColor = Link;
                link.ForeColor = Text;
            }
            else if ((c.Tag as string) == DimRole) c.ForeColor = DimText;
            else if ((c.Tag as string) == AccentRole) c.ForeColor = Accent;
            else if (c is GroupBox) c.ForeColor = Text;
        }

        private static void ApplyButton(Button b)
        {
            if (IsDark)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = HeaderBackground;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 72, 82);
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(78, 86, 98);
            }
            else
            {
                b.FlatStyle = FlatStyle.Standard;
                b.BackColor = SystemColors.Control;
                b.ForeColor = SystemColors.ControlText;
                b.UseVisualStyleBackColor = true;
            }
        }

        private static void ApplyGrid(DataGridView g)
        {
            g.BackgroundColor = Surface;
            g.GridColor = Border;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = Selection;
            g.DefaultCellStyle.SelectionForeColor = Text;
            g.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackground;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBackground;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
            g.ColumnHeadersBorderStyle = IsDark ? DataGridViewHeaderBorderStyle.Single : DataGridViewHeaderBorderStyle.Raised;
            SetControlTheme(g, "DarkMode_Explorer");
        }

        /// <summary>Menu podręczne (np. „Udostępnij”) w kolorach motywu.</summary>
        public static void Apply(ContextMenuStrip menu)
        {
            menu.Renderer = IsDark ? (ToolStripRenderer)new DarkToolStripRenderer() : new ToolStripProfessionalRenderer();
            menu.BackColor = IsDark ? Surface : SystemColors.Control;
            menu.ForeColor = Text;
        }

        #endregion

        #region Ciemny pasek tytułu i paski przewijania (Windows 10 1809+ / Windows 11)

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        private static void SetDarkTitleBar(Form form)
        {
            int value = IsDark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // Starszy Windows – pasek tytułu zostaje jasny.
            }
        }

        private static void SetControlTheme(Control c, string darkTheme)
        {
            if (!c.IsHandleCreated)
            {
                c.HandleCreated += (s, e) => SetControlTheme(c, darkTheme);
                return;
            }
            try
            {
                SetWindowTheme(c.Handle, IsDark ? darkTheme : "Explorer", null);
            }
            catch (DllNotFoundException)
            {
            }
        }

        #endregion
    }

    /// <summary>
    /// TabControl, który w ciemnym motywie rysuje zakładki sam – systemowy zawsze ma jasne tło.
    /// </summary>
    internal class ThemedTabControl : TabControl
    {
        private bool _userPaint;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdatePaintMode();
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            if (_userPaint != Theme.IsDark) UpdatePaintMode();
        }

        private void UpdatePaintMode()
        {
            _userPaint = Theme.IsDark;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, _userPaint);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_userPaint)
            {
                base.OnPaint(e);
                return;
            }

            var g = e.Graphics;
            using (var back = new SolidBrush(Theme.WindowBackground)) g.FillRectangle(back, ClientRectangle);

            var page = DisplayRectangle;
            page.Inflate(2, 2);
            using (var surface = new SolidBrush(Theme.Surface)) g.FillRectangle(surface, page);
            using (var border = new Pen(Theme.Border)) g.DrawRectangle(border, page.X, page.Y, page.Width - 1, page.Height - 1);

            for (int i = 0; i < TabCount; i++)
            {
                var rect = GetTabRect(i);
                bool selected = i == SelectedIndex;
                if (!selected) rect.Inflate(0, -1);
                using (var fill = new SolidBrush(selected ? Theme.Surface : Theme.HeaderBackground)) g.FillRectangle(fill, rect);
                using (var border = new Pen(Theme.Border)) g.DrawRectangle(border, rect.X, rect.Y, rect.Width - 1, rect.Height);
                TextRenderer.DrawText(g, TabPages[i].Text, Font, rect, selected ? Theme.Text : Theme.DimText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Wygląd pasków narzędzi i menu podręcznych w ciemnym motywie.</summary>
    internal class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.DimText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Text;
            base.OnRenderArrow(e);
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color MenuItemBorder { get { return Theme.Selection; } }
            public override Color MenuItemSelected { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Selection; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Selection; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
            public override Color StatusStripGradientBegin { get { return Theme.HeaderBackground; } }
            public override Color StatusStripGradientEnd { get { return Theme.HeaderBackground; } }
            public override Color ToolStripBorder { get { return Theme.Border; } }
            public override Color GripDark { get { return Theme.Border; } }
            public override Color GripLight { get { return Theme.Border; } }
        }
    }
}
