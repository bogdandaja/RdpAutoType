using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using KeePassLib;
using KeePassLib.Security;

namespace RdpAutoType
{
    /// <summary>
    /// Editor vizual de Login Script: in stanga lista de campuri (Fields), in
    /// dreapta secventa de autotype (Login Script) ca elemente K/F.
    /// Citeste si scrie stocarea nativa KeePass (pe.Strings + pe.AutoType).
    /// </summary>
    internal sealed class LoginScriptForm : Form
    {
        private sealed class FieldRow
        {
            public string Display;   // ce se vede la Name
            public string Token;     // pentru script: USERNAME / PASSWORD / S:Nume
            public string Value;
            public bool Protect;
            public bool Core;        // Username/Password - nu se sterg
        }

        private readonly PwEntry m_pe;
        private readonly PwDatabase m_db;

        private readonly List<FieldRow> m_fields = new List<FieldRow>();
        private readonly List<SeqElement> m_elems = new List<SeqElement>();
        private readonly List<string> m_origCustom = new List<string>();

        private ListView m_lvFields;
        private ListView m_lvScript;
        private ToolTip m_tip;
        private TextBox m_url;
        private SplitContainer m_split;
        private bool m_fixingCols;

        public LoginScriptForm(PwEntry pe, PwDatabase db)
        {
            m_pe = pe;
            m_db = db;
            DoubleBuffered = true;
            Opacity = 0;              // invizibila pana e asezata final (fara animatie)
            SuspendLayout();
            BuildUi();
            LoadFromEntry();
            RefreshFields();
            RefreshScript();
            ResumeLayout(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // asezare finala inainte de a o face vizibila
            if (m_split != null)
            {
                try
                {
                    int d = (int)(m_split.Width * 0.52);
                    if (d < m_split.Panel1MinSize) d = m_split.Panel1MinSize;
                    if (d > m_split.Width - m_split.Panel2MinSize - m_split.SplitterWidth)
                        d = m_split.Width - m_split.Panel2MinSize - m_split.SplitterWidth;
                    m_split.SplitterDistance = d;
                }
                catch { }
            }
            SizeFieldColumns();
            SizeScriptColumn();
            Opacity = 1;
        }

        // ---------------- UI ----------------

        private void BuildUi()
        {
            string title = m_pe.Strings.ReadSafe(PwDefs.TitleField);
            Text = "Edit Login Script" + (string.IsNullOrEmpty(title) ? "" : " - " + title);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(243, 243, 243);   // consistent, indiferent de tema
            ForeColor = Color.Black;
            ClientSize = new Size(840, 480);
            MinimumSize = new Size(640, 380);
            try { Icon = MakeAppIcon(32); } catch { }

            m_tip = new ToolTip();

            // dimensiuni derivate din inaltimea fontului -> identic proportional
            // pe orice DPI (nu depinde de 1.5 al vreunei masini).
            int btnH = U(1.5);
            int btnW = U(1.62);
            int vpad = Math.Max(2, U(0.16));
            int headerH = btnH + 2 * vpad;

            int top = 12;
            int webH = headerH + U(2.0);
            int secTop = top + webH + 10;
            int secH = ClientSize.Height - secTop - 54;

            // ---- Web Address (sus, full width) ----
            BuildWebAddress(new Rectangle(12, top, ClientSize.Width - 24, webH), headerH);

            // ---- Split redimensionabil Fields | Login Script ----
            SplitContainer split = new SplitContainer();
            split.Location = new Point(12, secTop);
            split.Size = new Size(ClientSize.Width - 24, secH);
            split.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            split.Orientation = Orientation.Vertical;   // bara verticala -> stanga/dreapta
            split.SplitterWidth = 6;
            split.Panel1MinSize = 200;
            split.Panel2MinSize = 220;
            split.BackColor = Color.White;
            split.Panel1.BackColor = Color.White;
            split.Panel2.BackColor = Color.White;
            Controls.Add(split);
            m_split = split;

            // Fields (stanga)
            FlowLayoutPanel tbF;
            Panel secF = Section("Fields", headerH, vpad, out m_lvFields, out tbF);
            m_lvFields.Columns.Add("Name", 150);
            m_lvFields.Columns.Add("Value", 240);
            SetupList(m_lvFields, false);
            m_lvFields.DoubleClick += delegate { EditField(); };
            m_lvFields.Resize += delegate { SizeFieldColumns(); };
            split.Panel1.Controls.Add(secF);

            tbF.Controls.Add(IconButton(ToolIcon("plus", btnW, btnH), "Add field", delegate { AddField(); }));
            tbF.Controls.Add(IconButton(ToolIcon("minus", btnW, btnH), "Remove field (double-click to edit)", delegate { RemoveField(); }));

            // Login Script (dreapta)
            FlowLayoutPanel tbS;
            Panel secS = Section("Login Script", headerH, vpad, out m_lvScript, out tbS);
            m_lvScript.HeaderStyle = ColumnHeaderStyle.None;
            m_lvScript.Columns.Add("", 330);
            SetupList(m_lvScript, true);
            m_lvScript.DoubleClick += delegate { EditScriptElement(); };
            m_lvScript.Resize += delegate { SizeScriptColumn(); };
            split.Panel2.Controls.Add(secS);

            tbS.Controls.Add(IconButton(ToolIcon("F", btnW, btnH), "Add field", delegate { AddScriptField(); }));
            tbS.Controls.Add(IconButton(ToolIcon("K", btnW, btnH), "Add key", delegate { AddScriptKey(); }));
            tbS.Controls.Add(IconButton(ToolIcon("tab", btnW, btnH), "Add Tab", delegate { InsertElement(SeqElement.NewSpecial("TAB", false, false, false, 1)); }));
            tbS.Controls.Add(IconButton(ToolIcon("enter", btnW, btnH), "Add Enter", delegate { InsertElement(SeqElement.NewSpecial("ENTER", false, false, false, 1)); }));
            tbS.Controls.Add(IconButton(ToolIcon("clock", btnW, btnH), "Add delay", delegate { AddScriptDelay(); }));
            tbS.Controls.Add(IconButton(ToolIcon("x", btnW, btnH), "Remove", delegate { RemoveScript(); }));

            // SplitterDistance + dimensionarea coloanelor se fac in OnShown,
            // dupa ce fereastra are marimea finala (fara flicker).

            // ---- OK / Cancel ----
            Button ok = new Button();
            ok.Text = "OK";
            ok.Size = new Size(90, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ok.Location = new Point(ClientSize.Width - 12 - 90 - 10 - 90, ClientSize.Height - 42);
            ok.Click += delegate { SaveToEntry(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(90, 30);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.Location = new Point(ClientSize.Width - 12 - 90, ClientSize.Height - 42);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static readonly Color FieldColor = Color.FromArgb(0, 150, 0);
        private static readonly Color KeyColor = Color.FromArgb(0, 90, 200);
        private static readonly Color DelayColor = Color.FromArgb(160, 110, 0);
        private static readonly Color TextColor = Color.FromArgb(110, 110, 110);
        private static readonly Color HeaderColor = Color.FromArgb(124, 139, 154);  // banda ceva mai deschisa
        private static readonly Color TitleColor = Color.FromArgb(255, 210, 70);    // titluri galbene

        /// <summary>Unitate relativa la font (DPI-independent).</summary>
        private int U(double f) { return (int)Math.Round(Font.Height * f); }

        /// <summary>O sectiune cu bara de titlu (titlu la stanga, butoane la
        /// dreapta, centrate vertical) si un ListView care umple restul.</summary>
        private Panel Section(string title, int headerH, int vpad,
            out ListView lv, out FlowLayoutPanel toolbar)
        {
            Panel sec = new Panel();
            sec.Dock = DockStyle.Fill;
            sec.BackColor = Color.White;
            sec.BorderStyle = BorderStyle.FixedSingle;

            Panel hdr = new Panel();
            hdr.Dock = DockStyle.Top;
            hdr.Height = headerH;
            hdr.BackColor = HeaderColor;

            FlowLayoutPanel tb = new FlowLayoutPanel();
            tb.Dock = DockStyle.Right;
            tb.AutoSize = true;
            tb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tb.FlowDirection = FlowDirection.LeftToRight;
            tb.WrapContents = false;
            tb.BackColor = HeaderColor;
            tb.Padding = new Padding(0, vpad, 4, vpad);   // centreaza butoanele vertical
            tb.Margin = Padding.Empty;

            Label t = new Label();
            t.Text = title;
            t.Dock = DockStyle.Fill;
            t.ForeColor = TitleColor;
            t.Font = new Font(Font, FontStyle.Bold);
            t.TextAlign = ContentAlignment.MiddleLeft;
            t.AutoEllipsis = false;
            t.Padding = new Padding(8, 0, 0, 0);

            hdr.Controls.Add(t);   // Fill
            hdr.Controls.Add(tb);  // Right

            lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.Dock = DockStyle.Fill;
            lv.BackColor = Color.White;      // consistent, indiferent de tema
            lv.BorderStyle = BorderStyle.None;

            sec.Controls.Add(lv);   // Fill (adaugat primul)
            sec.Controls.Add(hdr);  // Top  (adaugat ultimul -> sus)

            toolbar = tb;
            return sec;
        }

        // ---------------- owner-draw liste (compact + bold, DPI-independent) ----------------

        private void SetupList(ListView lv, bool script)
        {
            lv.OwnerDraw = true;
            int rowH = lv.Font.Height;                    // cat mai strins
            if (rowH < 13) rowH = 13;

            ImageList sizer = new ImageList();            // forteaza inaltimea rindului
            sizer.ImageSize = new Size(1, Math.Min(256, rowH));
            sizer.ColorDepth = ColorDepth.Depth32Bit;
            lv.SmallImageList = sizer;

            lv.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e) { e.DrawDefault = true; };
            lv.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e) { e.DrawDefault = false; };

            // la redimensionarea unei coloane: repicteaza tot (altfel raman
            // artefacte in tema dark) si pastreaza ultima coloana pe toata latimea.
            ListView lvc = lv;
            bool isScript = script;
            lvc.ColumnWidthChanged += delegate
            {
                if (m_fixingCols) return;
                m_fixingCols = true;
                try
                {
                    if (isScript) SizeScriptColumn();
                    else { int w = lvc.ClientSize.Width; lvc.Columns[1].Width = Math.Max(40, w - lvc.Columns[0].Width - 2); }
                }
                catch { }
                m_fixingCols = false;
                lvc.Invalidate();
            };

            if (script)
            {
                lv.DrawItem += ScriptDrawItem;
                // reordonare prin drag-and-drop
                lv.AllowDrop = true;
                lv.ItemDrag += delegate(object s, ItemDragEventArgs e) { lv.DoDragDrop(e.Item, DragDropEffects.Move); };
                lv.DragEnter += delegate(object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
                lv.DragOver += delegate(object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
                lv.DragDrop += ScriptDragDrop;
            }
            else lv.DrawItem += FieldsDrawItem;
        }

        private void ScriptDragDrop(object sender, DragEventArgs e)
        {
            ListViewItem dragged = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
            if (dragged == null) return;
            int from = dragged.Index;
            if (from < 0 || from >= m_elems.Count) return;

            Point p = m_lvScript.PointToClient(new Point(e.X, e.Y));
            ListViewItem over = m_lvScript.GetItemAt(p.X, p.Y);

            int to;
            if (over == null)
                to = m_elems.Count - 1;         // sub ultimul -> la final
            else
            {
                to = over.Index;
                Rectangle rb = over.GetBounds(ItemBoundsPortion.Entire);
                if (p.Y > rb.Top + rb.Height / 2) to++;   // dupa elementul tinta
                if (from < to) to--;                      // compenseaza stergerea
            }
            if (to < 0) to = 0;
            if (to > m_elems.Count - 1) to = m_elems.Count - 1;
            if (to == from) return;

            SeqElement el = m_elems[from];
            m_elems.RemoveAt(from);
            m_elems.Insert(to, el);
            RefreshScript();
            if (to < m_lvScript.Items.Count) m_lvScript.Items[to].Selected = true;
        }

        private void ScriptDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            bool sel = e.Item.Selected;
            using (SolidBrush bg = new SolidBrush(sel ? SystemColors.Highlight : Color.White))
                g.FillRectangle(bg, r);

            SeqElement el = e.Item.Tag as SeqElement;
            if (el == null) return;
            Color col = (el.Kind == SeqKind.Field) ? FieldColor
                      : (el.Kind == SeqKind.Key) ? KeyColor
                      : (el.Kind == SeqKind.Delay) ? DelayColor : TextColor;

            int bs = r.Height - 2; if (bs < 10) bs = 10;
            using (Bitmap badge = Badge(el.Badge, col, bs))
                g.DrawImage(badge, r.Left + 4, r.Top + (r.Height - bs) / 2, bs, bs);

            Rectangle tr = new Rectangle(r.Left + 4 + bs + 6, r.Top, r.Width - (bs + 14), r.Height);
            using (Font bf = new Font(e.Item.ListView.Font, FontStyle.Bold))
                TextRenderer.DrawText(g, el.Display, bf, tr,
                    sel ? SystemColors.HighlightText : Color.Black,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void FieldsDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            ListView lv = e.Item.ListView;
            bool sel = e.Item.Selected;
            using (SolidBrush bg = new SolidBrush(sel ? SystemColors.Highlight : Color.White))
                g.FillRectangle(bg, r);

            FieldRow fr = e.Item.Tag as FieldRow;
            if (fr == null) return;
            int c0 = lv.Columns[0].Width;
            Color fg = sel ? SystemColors.HighlightText : Color.Black;

            string shown = fr.Protect
                ? new string('●', Math.Min(10, Math.Max(3, (fr.Value ?? "").Length)))
                : fr.Value;

            using (Font bf = new Font(lv.Font, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, fr.Display, bf,
                    new Rectangle(r.Left + 6, r.Top, c0 - 8, r.Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, shown, bf,
                    new Rectangle(r.Left + c0 + 4, r.Top, r.Width - c0 - 8, r.Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>Bara "Web Address" + campul URL centrat vertical dedesubt.</summary>
        private void BuildWebAddress(Rectangle bounds, int headerH)
        {
            Panel sec = new Panel();
            sec.Bounds = bounds;
            sec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sec.BackColor = Color.White;
            sec.BorderStyle = BorderStyle.FixedSingle;

            Panel hdr = new Panel();
            hdr.Dock = DockStyle.Top;
            hdr.Height = headerH;
            hdr.BackColor = HeaderColor;

            Label t = new Label();
            t.Text = "Web Address";
            t.Dock = DockStyle.Fill;
            t.ForeColor = TitleColor;
            t.Font = new Font(Font, FontStyle.Bold);
            t.TextAlign = ContentAlignment.MiddleLeft;
            t.Padding = new Padding(8, 0, 0, 0);
            hdr.Controls.Add(t);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.White;
            body.Padding = new Padding(8, 0, 8, 0);

            m_url = new TextBox();
            m_url.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            m_url.BorderStyle = BorderStyle.None;   // fara chenarul alb
            m_url.BackColor = Color.White;
            body.Controls.Add(m_url);

            EventHandler center = delegate
            {
                m_url.Left = body.Padding.Left;
                m_url.Width = Math.Max(10, body.ClientSize.Width - body.Padding.Left - body.Padding.Right);
                m_url.Top = Math.Max(0, (body.ClientSize.Height - m_url.Height) / 2);
            };
            body.Resize += center;
            body.SizeChanged += center;

            sec.Controls.Add(body);   // Fill
            sec.Controls.Add(hdr);    // Top
            Controls.Add(sec);
            center(null, null);
        }

        private Button IconButton(Image img, string tip, EventHandler onClick)
        {
            Button b = new Button();
            b.Image = img;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;   // chenarul e desenat in imagine
            b.BackColor = HeaderColor;         // se contopeste cu bara -> transparent
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(150, 165, 180);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(165, 180, 195);
            b.Size = img.Size;
            b.Margin = new Padding(2, 0, 0, 0);   // centrarea verticala o face bara
            b.Padding = Padding.Empty;
            b.ImageAlign = ContentAlignment.MiddleCenter;
            b.TabStop = false;
            b.Click += onClick;
            if (m_tip != null) m_tip.SetToolTip(b, tip);
            return b;
        }

        private void SizeFieldColumns()
        {
            if (m_lvFields == null || m_lvFields.Columns.Count < 2) return;
            int w = m_lvFields.ClientSize.Width;
            m_lvFields.Columns[0].Width = (int)(w * 0.42);
            m_lvFields.Columns[1].Width = w - m_lvFields.Columns[0].Width - 2;
        }

        private void SizeScriptColumn()
        {
            if (m_lvScript == null || m_lvScript.Columns.Count < 1) return;
            m_lvScript.Columns[0].Width = m_lvScript.ClientSize.Width - 2;
        }

        private static readonly Color IconDark = Color.FromArgb(70, 70, 70);

        // ---------------- icoane desenate (scalabile, fara font de simboluri) ----------------

        private static readonly Color RemoveColor = Color.FromArgb(190, 40, 40);

        private static Color FrameColorFor(string kind)
        {
            switch (kind)
            {
                case "F": case "plus": return FieldColor;
                case "K": case "tab": case "enter": return KeyColor;
                case "clock": return DelayColor;
                case "up": case "down": return IconDark;
                case "minus": case "x": return RemoveColor;
                default: return IconDark;
            }
        }

        /// <summary>Iconita de toolbar: chenar rotunjit, gros (2px), tras 1px spre
        /// interior, colorat dupa iconita; glifa desenata in interior.</summary>
        private static Bitmap ToolIcon(string kind, int w, int h)
        {
            Color fc = FrameColorFor(kind);
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float bw = 2f;                 // grosime dubla (era 1)
                float ins = 1f + bw / 2f;      // 1px de margine, spre interior
                RectangleF r = new RectangleF(ins, ins, w - 1 - 2 * ins, h - 1 - 2 * ins);
                using (var path = RoundRect(r, Math.Min(w, h) * 0.22f))
                using (Pen p = new Pen(fc, bw))
                    g.DrawPath(p, path);   // fara umplutura -> transparent

                // Glifa interioara proportionala cu butonul (DPI-independent):
                // literele F/K mai mari, formele putin mai mici si centrate.
                bool isLetter = (kind == "F" || kind == "K");
                int inner = (int)Math.Round(Math.Min(w, h) * (isLetter ? 0.80 : 0.58));
                if (inner < 8) inner = 8;
                using (Bitmap gl = Glyph(kind, fc, inner))
                    g.DrawImage(gl, (w - inner) / 2, (h - inner) / 2, inner, inner);
            }
            return bmp;
        }

        private static Bitmap Glyph(string kind, Color color, int size)
        {
            switch (kind)
            {
                case "F": return LetterGlyph("F", color, size);
                case "K": return LetterGlyph("K", color, size);
                case "tab": return IconTab(size);
                case "enter": return IconEnter(size);
                case "clock": return IconClock(size);
                case "up": return IconUp(size);
                case "down": return IconDown(size);
                case "plus": return IconPlus(size);
                case "minus": return IconMinus(size);
                case "x": return IconX(size);
                default: return new Bitmap(size, size);
            }
        }

        private static Bitmap LetterGlyph(string letter, Color color, int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                DrawHeavyLetter(g, letter, color, size * 0.72f, new RectangleF(0, 0, size, size));
            }
            return bmp;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>Bitmap-ul de identitate: patrat rotunjit, contur albastru,
        /// doua chevroane (primul albastru, al doilea rosu) = ">>", autotype.
        /// Folosit pentru icoana ferestrei si pentru item-ul de meniu.</summary>
        internal static Bitmap MakeIconBitmap(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float bw = Math.Max(1.4f, size * 0.07f);
                float ins = bw / 2f + size * 0.04f;
                RectangleF r = new RectangleF(ins, ins, size - 1 - 2 * ins, size - 1 - 2 * ins);
                using (var path = RoundRect(r, size * 0.22f))
                {
                    using (SolidBrush bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
                    using (Pen p = new Pen(KeyColor, bw)) g.DrawPath(p, path);
                }
                Chevron(g, size, 0.32f, KeyColor);
                Chevron(g, size, 0.52f, Color.FromArgb(200, 40, 40));
            }
            return bmp;
        }

        private static Icon MakeAppIcon(int size)
        {
            using (Bitmap bmp = MakeIconBitmap(size))
            {
                IntPtr h = bmp.GetHicon();
                Icon ic = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                return ic;
            }
        }

        private static void Chevron(Graphics g, int S, float x, Color col)
        {
            using (Pen p = new Pen(col, S * 0.09f))
            {
                p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                PointF a = new PointF(S * x, S * 0.30f);
                PointF b = new PointF(S * (x + 0.15f), S * 0.5f);
                PointF c = new PointF(S * x, S * 0.70f);
                g.DrawLines(p, new PointF[] { a, b, c });
            }
        }

        /// <summary>Litera "grea" (bold): contur umplut + stroke, nu subtire.</summary>
        private static void DrawHeavyLetter(Graphics g, string letter, Color col, float emPx, RectangleF rect)
        {
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                using (FontFamily ff = new FontFamily("Segoe UI"))
                    path.AddString(letter, ff, (int)FontStyle.Bold, emPx, rect, sf);
                using (SolidBrush fb = new SolidBrush(col))
                    g.FillPath(fb, path);
                using (Pen pen = new Pen(col, Math.Max(0.6f, emPx * 0.10f)))
                {
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawPath(pen, path);
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(RectangleF r, float rad)
        {
            float d = rad * 2;
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Badge: patrat rotunjit, fundal alb, contur si
        /// litera colorate.</summary>
        private static Bitmap Badge(string letter, Color col, int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float border = Math.Max(1.6f, size * 0.10f);
                float margin = 1.5f;                 // contur la ~1-2px de margine
                float inset = margin + border / 2f;
                RectangleF r = new RectangleF(inset, inset, size - 1 - 2 * inset, size - 1 - 2 * inset);
                using (var path = RoundRect(r, size * 0.22f))
                {
                    using (SolidBrush bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
                    using (Pen p = new Pen(col, border)) g.DrawPath(p, path);
                }
                DrawHeavyLetter(g, letter, col, size * 0.62f, new RectangleF(0, 0, size, size));
            }
            return bmp;
        }

        private static Bitmap NewIcon(out Graphics g, int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size / 16f;
            g.ScaleTransform(s, s);   // desenam in grila 16x16, scalat la size
            return bmp;
        }

        private static Bitmap IconPlus(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(FieldColor, 2.3f))
            {
                g.DrawLine(p, 8, 3, 8, 13);
                g.DrawLine(p, 3, 8, 13, 8);
            }
            g.Dispose(); return b;
        }

        private static Bitmap IconMinus(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(Color.FromArgb(190, 40, 40), 2.3f))
                g.DrawLine(p, 3, 8, 13, 8);
            g.Dispose(); return b;
        }

        private static Bitmap IconPencil(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(IconDark, 2f))
                g.DrawLine(p, 3, 13, 11, 5);
            using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 160, 0)))
                g.FillPolygon(br, new Point[] { new Point(11, 5), new Point(13, 7), new Point(12, 3) });
            g.Dispose(); return b;
        }

        private static Bitmap IconTab(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(KeyColor, 2f))
            {
                g.DrawLine(p, 2, 8, 9, 8);          // shaft
                g.DrawLine(p, 13, 4, 13, 12);       // bara
            }
            using (SolidBrush br = new SolidBrush(KeyColor))
                g.FillPolygon(br, new Point[] { new Point(9, 4), new Point(9, 12), new Point(13, 8) });
            g.Dispose(); return b;
        }

        private static Bitmap IconEnter(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(KeyColor, 2f))
            {
                g.DrawLine(p, 13, 3, 13, 8);        // coltul vertical
                g.DrawLine(p, 4, 8, 13, 8);         // shaft orizontal
            }
            using (SolidBrush br = new SolidBrush(KeyColor))
                g.FillPolygon(br, new Point[] { new Point(4, 8), new Point(8, 4), new Point(8, 12) });
            g.Dispose(); return b;
        }

        private static Bitmap IconClock(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(DelayColor, 1.7f))
            {
                g.DrawEllipse(p, 2, 2, 11, 11);
                g.DrawLine(p, 8, 8, 8, 4);
                g.DrawLine(p, 8, 8, 11, 9);
            }
            g.Dispose(); return b;
        }

        private static Bitmap IconUp(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (SolidBrush br = new SolidBrush(IconDark))
                g.FillPolygon(br, new Point[] { new Point(8, 4), new Point(3, 11), new Point(13, 11) });
            g.Dispose(); return b;
        }

        private static Bitmap IconDown(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (SolidBrush br = new SolidBrush(IconDark))
                g.FillPolygon(br, new Point[] { new Point(3, 5), new Point(13, 5), new Point(8, 12) });
            g.Dispose(); return b;
        }

        private static Bitmap IconX(int size)
        {
            Graphics g; Bitmap b = NewIcon(out g, size);
            using (Pen p = new Pen(Color.FromArgb(190, 40, 40), 2.2f))
            {
                g.DrawLine(p, 4, 4, 12, 12);
                g.DrawLine(p, 12, 4, 4, 12);
            }
            g.Dispose(); return b;
        }

        // ---------------- load ----------------

        private void LoadFromEntry()
        {
            m_url.Text = m_pe.Strings.ReadSafe(PwDefs.UrlField);

            m_fields.Add(new FieldRow
            {
                Display = "User name",
                Token = "USERNAME",
                Value = m_pe.Strings.ReadSafe(PwDefs.UserNameField),
                Protect = false,
                Core = true
            });
            m_fields.Add(new FieldRow
            {
                Display = "Password",
                Token = "PASSWORD",
                Value = m_pe.Strings.ReadSafe(PwDefs.PasswordField),
                Protect = true,
                Core = true
            });

            foreach (KeyValuePair<string, ProtectedString> kv in m_pe.Strings)
            {
                if (PwDefs.IsStandardField(kv.Key)) continue;
                m_origCustom.Add(kv.Key);
                m_fields.Add(new FieldRow
                {
                    Display = kv.Key,
                    Token = "S:" + kv.Key,
                    Value = kv.Value.ReadString(),
                    Protect = kv.Value.IsProtected,
                    Core = false
                });
            }

            string seq = null;
            try { seq = (m_pe.AutoType != null) ? m_pe.AutoType.DefaultSequence : null; }
            catch { }

            if (!string.IsNullOrEmpty(seq))
                m_elems.AddRange(SeqCodec.Parse(seq));
            else
            {
                // Default: Ctrl+A goleste campul inainte de fiecare tastare.
                m_elems.Add(SeqElement.NewChar('a', true, false, false));   // Ctrl+A
                m_elems.Add(SeqElement.NewField("USERNAME"));
                m_elems.Add(SeqElement.NewSpecial("TAB", false, false, false, 1));
                m_elems.Add(SeqElement.NewChar('a', true, false, false));   // Ctrl+A
                m_elems.Add(SeqElement.NewField("PASSWORD"));
                m_elems.Add(SeqElement.NewSpecial("ENTER", false, false, false, 1));
            }
        }

        // ---------------- refresh ----------------

        private void RefreshFields()
        {
            m_lvFields.BeginUpdate();
            m_lvFields.Items.Clear();
            foreach (FieldRow r in m_fields)
            {
                string shown = r.Protect ? new string('●', Math.Min(10, Math.Max(3, r.Value.Length))) : r.Value;
                ListViewItem it = new ListViewItem(r.Display);
                it.SubItems.Add(shown);
                it.Tag = r;
                m_lvFields.Items.Add(it);
            }
            m_lvFields.EndUpdate();
        }

        private void RefreshScript()
        {
            m_lvScript.BeginUpdate();
            m_lvScript.Items.Clear();
            foreach (SeqElement e in m_elems)
            {
                ListViewItem it = new ListViewItem(e.Display);
                it.Tag = e;
                m_lvScript.Items.Add(it);
            }
            m_lvScript.EndUpdate();
        }

        // ---------------- Fields actions ----------------

        private FieldRow SelectedField()
        {
            if (m_lvFields.SelectedItems.Count == 0) return null;
            return m_lvFields.SelectedItems[0].Tag as FieldRow;
        }

        private void AddField()
        {
            using (FieldEditDialog d = new FieldEditDialog("", "", false, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (string.IsNullOrEmpty(d.FieldName)) return;
                m_fields.Add(new FieldRow
                {
                    Display = d.FieldName,
                    Token = "S:" + d.FieldName,
                    Value = d.FieldValue,
                    Protect = d.Protect,
                    Core = false
                });
                RefreshFields();
            }
        }

        private void EditField()
        {
            FieldRow r = SelectedField();
            if (r == null) return;
            using (FieldEditDialog d = new FieldEditDialog(r.Display, r.Value, r.Protect, !r.Core))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!r.Core)
                {
                    r.Display = d.FieldName;
                    r.Token = "S:" + d.FieldName;
                }
                r.Protect = d.Protect;   // si pentru core: starea Hide se salveaza
                r.Value = d.FieldValue;
                RefreshFields();
            }
        }

        private void RemoveField()
        {
            FieldRow r = SelectedField();
            if (r == null) return;
            if (r.Core)
            {
                MessageBox.Show("Username and Password cannot be removed.", "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            m_fields.Remove(r);
            RefreshFields();
        }

        // ---------------- Script actions ----------------

        private int SelectedScriptIndex()
        {
            if (m_lvScript.SelectedIndices.Count == 0) return -1;
            return m_lvScript.SelectedIndices[0];
        }

        private void InsertElement(SeqElement e)
        {
            int idx = SelectedScriptIndex();
            if (idx < 0) m_elems.Add(e);
            else m_elems.Insert(idx + 1, e);
            RefreshScript();
            int sel = (idx < 0) ? m_elems.Count - 1 : idx + 1;
            if (sel >= 0 && sel < m_lvScript.Items.Count)
                m_lvScript.Items[sel].Selected = true;
        }

        private void AddScriptField()
        {
            List<string> custom = new List<string>();
            foreach (FieldRow r in m_fields)
                if (!r.Core) custom.Add(r.Display);

            using (FieldPickDialog d = new FieldPickDialog(custom, null))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                InsertElement(SeqElement.NewField(d.Token));
            }
        }

        /// <summary>Dublu-click pe un element: il deschide in editorul potrivit,
        /// preincarcat, si il inlocuieste.</summary>
        private void EditScriptElement()
        {
            int idx = SelectedScriptIndex();
            if (idx < 0 || idx >= m_elems.Count) return;
            SeqElement el = m_elems[idx];

            switch (el.Kind)
            {
                case SeqKind.Field:
                    {
                        List<string> custom = new List<string>();
                        foreach (FieldRow r in m_fields) if (!r.Core) custom.Add(r.Display);
                        using (FieldPickDialog d = new FieldPickDialog(custom, el.FieldToken))
                            if (d.ShowDialog(this) == DialogResult.OK)
                                m_elems[idx] = SeqElement.NewField(d.Token);
                        break;
                    }
                case SeqKind.Key:
                    using (KeyPickDialog d = new KeyPickDialog(el))
                        if (d.ShowDialog(this) == DialogResult.OK)
                            m_elems[idx] = d.Result;
                    break;
                case SeqKind.Delay:
                    using (DelayDialog d = new DelayDialog(el.DelayMs))
                        if (d.ShowDialog(this) == DialogResult.OK)
                            m_elems[idx] = SeqElement.NewDelay(d.DelayMs);
                    break;
                case SeqKind.Text:
                    using (TextInputDialog d = new TextInputDialog(el.Text))
                        if (d.ShowDialog(this) == DialogResult.OK)
                            m_elems[idx] = SeqElement.NewText(d.Value);
                    break;
            }

            RefreshScript();
            if (idx < m_lvScript.Items.Count) m_lvScript.Items[idx].Selected = true;
        }

        private void AddScriptKey()
        {
            using (KeyPickDialog d = new KeyPickDialog())
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                InsertElement(d.Result);
            }
        }

        private void AddScriptDelay()
        {
            using (DelayDialog d = new DelayDialog(500))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                InsertElement(SeqElement.NewDelay(d.DelayMs));
            }
        }

        private void RemoveScript()
        {
            int idx = SelectedScriptIndex();
            if (idx < 0) return;
            m_elems.RemoveAt(idx);
            RefreshScript();
            if (m_elems.Count > 0)
                m_lvScript.Items[Math.Min(idx, m_elems.Count - 1)].Selected = true;
        }

        private void MoveScript(int dir)
        {
            int idx = SelectedScriptIndex();
            if (idx < 0) return;
            int ni = idx + dir;
            if (ni < 0 || ni >= m_elems.Count) return;
            SeqElement tmp = m_elems[idx];
            m_elems[idx] = m_elems[ni];
            m_elems[ni] = tmp;
            RefreshScript();
            m_lvScript.Items[ni].Selected = true;
        }

        // ---------------- save ----------------

        private void SaveToEntry()
        {
            try { m_pe.CreateBackup(m_db); }
            catch { }

            // URL (Web Address)
            m_pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, m_url.Text ?? ""));

            // campuri core (respecta starea Hide aleasa)
            SetCore(PwDefs.UserNameField, FindToken("USERNAME"));
            SetCore(PwDefs.PasswordField, FindToken("PASSWORD"));

            // campuri custom: scrie cele curente
            List<string> now = new List<string>();
            foreach (FieldRow r in m_fields)
            {
                if (r.Core || string.IsNullOrEmpty(r.Display)) continue;
                m_pe.Strings.Set(r.Display, new ProtectedString(r.Protect, r.Value ?? ""));
                now.Add(r.Display);
            }
            // sterge custom-urile originale care nu mai exista
            foreach (string name in m_origCustom)
                if (!now.Contains(name))
                    m_pe.Strings.Remove(name);

            // secventa
            try
            {
                if (m_pe.AutoType != null)
                {
                    m_pe.AutoType.DefaultSequence = SeqCodec.Build(m_elems);
                    m_pe.AutoType.Enabled = true;
                }
            }
            catch { }

            try { m_pe.Touch(true); } catch { }
        }

        private FieldRow FindToken(string token)
        {
            foreach (FieldRow r in m_fields)
                if (r.Token == token) return r;
            return null;
        }

        private void SetCore(string field, FieldRow r)
        {
            if (r == null) return;
            m_pe.Strings.Set(field, new ProtectedString(r.Protect, r.Value ?? ""));
        }
    }
}
