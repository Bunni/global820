﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace global820

{
    public partial class Global820 : Form
    {
        private int currentLine = 0;
        private System.Timers.Timer tmr;
        private bool filterBroken = false;
        private long startPos = 0;
        System.Media.SoundPlayer notify;

        public Global820()
        {
            InitializeComponent();
            chat.AutoScroll = false;
            chat.HorizontalScroll.Enabled = false;
            chat.HorizontalScroll.Visible = false;
            chat.HorizontalScroll.Maximum = 0;
            chat.VerticalScroll.Value = 0;
            chat.AutoScroll = true;

            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            System.IO.Stream s = a.GetManifestResourceStream("global820.notify.wav");
            notify = new System.Media.SoundPlayer(s);
        }
        private Control processLine(string line) {
            Panel pnl = null;
            Match match = Regex.Match(line, ".{20}[^]]*] (.*)");
            if (match.Success) {
                //char type = match.Groups[1].Value[0];
                //if (type=='@' || type =='#' || type=='$') {
                //we only want the global chat
                if (match.Groups[1].Value[0] == '#') {
                    string[] data = match.Groups[1].Value.Split(new string[] { ": " }, 2, StringSplitOptions.None);

                    //use the filters
                    string[] filters = Properties.Settings.Default.Filter.Split(new string[] { System.Environment.NewLine },StringSplitOptions.RemoveEmptyEntries);
                    foreach (string filter in filters) {
                        try {
                            bool m = Regex.Match(data[1], filter,RegexOptions.IgnoreCase).Success;
                            if (Properties.Settings.Default.Whitelist) {
                                m = !m;
                            }
                            if (m) {
                                return null;
                            }
                        } catch (Exception e) {
                            MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK,MessageBoxIcon.Error);
                            filterBroken = true;
                            tmr.Stop();
                        }
                    }

                    pnl = new Panel();
                    pnl.MouseLeave += pnl_MouseLeave;
                    pnl.MouseEnter += pnl_MouseEnter;
                    pnl.Click += pnl_Click;
                    pnl.Font = new Font(pnl.Font.FontFamily, pnl.Font.Size * 1.2f);

                    Label name = new Label();
                    name.Name = "name";
                    pnl.Controls.Add(name);
                    name.ForeColor = Color.DarkRed;
                    name.Font = new Font(pnl.Font.FontFamily, pnl.Font.Size * 1f, FontStyle.Bold);
                    name.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Top;
                    name.Top = 0;
                    name.Left = 0;
                    name.AutoSize = true;
                    name.Text = data[0].Substring(1);
                    pnl.Height = name.Height; //use autosize
                    name.AutoSize = false;
                    name.Height = pnl.Height;
                    name.MouseLeave += pnl_MouseLeave;
                    name.MouseEnter += pnl_MouseEnter;
                    name.Click += pnl_Click;

                    Label txt = new Label();
                    txt.Name = "txt";
                    pnl.Controls.Add(txt);
                    txt.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                    txt.Top = 0;
                    txt.Left = name.Width;
                    txt.Width = pnl.Width - name.Width;
                    txt.Height = pnl.Height;
                    txt.Text = data[1];
                    txt.MouseLeave += pnl_MouseLeave;
                    txt.MouseEnter += pnl_MouseEnter;
                    txt.Click += pnl_Click;

                    //check for notifications
                    string[] notifyList = Properties.Settings.Default.NotifyList.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string item in notifyList) {
                        if (Regex.Match(data[1], item, RegexOptions.IgnoreCase).Success) {
                            notify.Play();
                            pnl.BackColor = SystemColors.GradientInactiveCaption;
                        }
                    }
                }
            }

            return pnl;
        }

        void pnl_Click(object sender, EventArgs e) {
            string name;
            if (sender is Panel) {
                name = ((Panel)sender).Controls.Find("name", false)[0].Text;
            } else {
                name = ((Panel)((Control)sender).Parent).Controls.Find("name", false)[0].Text;
            }
            Clipboard.SetText("@"+Regex.Replace(name,"^<.*> ","")+" Hi, still got room for me?");
        }

        void pnl_MouseLeave(object sender, EventArgs e) {
            if (sender is Panel) {
                ((Panel)sender).BackColor = SystemColors.Control;
            } else {
                ((Panel)((Control)sender).Parent).BackColor = SystemColors.Control;
            }
        }

        void pnl_MouseEnter(object sender, EventArgs e) {
            if (sender is Panel) {
                ((Panel)sender).BackColor = SystemColors.ControlLightLight;
            } else {
                ((Panel)((Control)sender).Parent).BackColor = SystemColors.ControlLightLight;
            }
        }

        private void tmr_Elapsed(object source, System.Timers.ElapsedEventArgs e) {
            if (!File.Exists(Properties.Settings.Default.LogPath)) {
                tmr.Stop();
                return;
            }

            using (Stream s = new FileStream(Path.GetFullPath(Properties.Settings.Default.LogPath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                bool suppressNotifications = false;
                if (startPos <= 0) {
                    startPos = s.Length - 1024;
                }
                s.Position = startPos;
                StreamReader rdr = new StreamReader(s);
                for (int i = 0; i < currentLine; i++) {
                    if (rdr.EndOfStream) break;
                    rdr.ReadLine();
                }

                while (!rdr.EndOfStream && !filterBroken) {
                    Control lbl = processLine(rdr.ReadLine());
                    if (lbl != null) {
                        chat.Invoke(new ThreadStart(delegate {
                            lbl.Width = chat.Width - 25;
                            lbl.Top = chat.Controls.Count * (lbl.Height+1) - chat.VerticalScroll.Value;
                            lbl.Left = 0;
                            chat.Controls.Add(lbl);
                            chat.ScrollControlIntoView(lbl);
                        }));
                    }

                    currentLine++;
                }
            }
        }

        private void chat_Resize(object sender, EventArgs e)
        {
            foreach (Control ctrl in chat.Controls)
            {
                ctrl.Width = chat.Width-25;
            }
        }

        private void chat_MouseEnter(object sender, EventArgs e) {
            chat.Focus();
        }

        private void settings_Click(object sender, EventArgs e) {
            Settings settings = new Settings();
            if (tmr != null && tmr.Enabled) {
                tmr.Stop();
            }
            settings.ShowDialog();
            start();
        }

        private void start() {
            if (Properties.Settings.Default.LogPath != "" && File.Exists(Properties.Settings.Default.LogPath)) {
                tmr = new System.Timers.Timer(Properties.Settings.Default.Polling);
                tmr.Elapsed += tmr_Elapsed;
                filterBroken = false;
                chat.Controls.Clear();
                currentLine = 0;
                startPos = 0;
                tmr_Elapsed(null, null);
                tmr.Start();
            } else {
                MessageBox.Show("Select your client logfile in the settings");
            }
        }

        private void Global820_Shown(object sender, EventArgs e) {
            start();
        }
    }
}
