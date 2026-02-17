using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

[assembly: System.Reflection.AssemblyTitle("MSP Network Diagnostics")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace MspDiag
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DiagForm());
        }
    }

    static class C
    {
        public static readonly Color BG       = Color.FromArgb(18,  18,  30);
        public static readonly Color Panel    = Color.FromArgb(28,  28,  46);
        public static readonly Color Border   = Color.FromArgb(50,  50,  75);
        public static readonly Color TextMain = Color.FromArgb(220, 225, 240);
        public static readonly Color TextDim  = Color.FromArgb(130, 135, 160);
        public static readonly Color Accent   = Color.FromArgb(80,  130, 255);
        public static readonly Color Green    = Color.FromArgb(60,  200, 120);
        public static readonly Color Yellow   = Color.FromArgb(250, 190,  60);
        public static readonly Color Red      = Color.FromArgb(240,  70,  80);
        public static readonly Color BtnBg    = Color.FromArgb(45,  45,  70);
        public static readonly Color BtnHover = Color.FromArgb(80,  130, 255);
    }

    class FlatButton : Button
    {
        bool _hover;
        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = C.BtnBg;
            ForeColor = C.TextMain;
            Font      = new Font("Segoe UI", 9f, FontStyle.Regular);
            Cursor    = Cursors.Hand;
            FlatAppearance.BorderSize  = 1;
            FlatAppearance.BorderColor = C.Border;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover=true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(_hover ? C.BtnHover : BackColor);
            FlatAppearance.BorderColor = _hover ? C.BtnHover : C.Border;
            base.OnPaint(e);
        }
    }

    class PingResult  { public string Host; public bool Ok; public long Ms; }
    class DnsResult   { public string Label; public bool Ok; public string Result; public long Ms; }
    class DiskRow     { public string Drive; public float ReadMBs; public float WriteMBs; public float FreeGB; }

    class DiagForm : Form
    {
        RichTextBox   _rtb;
        Label         _statusLabel;
        Panel         _statusBar;
        FlatButton    _btnRun, _btnCopy, _btnClose;
        StringBuilder _plainLog = new StringBuilder();
        Thread        _worker;

        public DiagForm()
        {
            Text          = "MSP Network Diagnostics  -  CodeBlue";
            Size          = new Size(820, 700);
            MinimumSize   = new Size(680, 580);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = C.BG;
            ForeColor     = C.TextMain;
            Font          = new Font("Segoe UI", 9f);
            BuildUI();
            Shown += delegate { RunDiagnostics(); };
        }

        void BuildUI()
        {
            _rtb = new RichTextBox {
                BackColor   = C.Panel,
                ForeColor   = C.TextMain,
                Font        = new Font("Consolas", 9f),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                WordWrap    = false,
                Anchor      = AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,
                Location    = new Point(12, 8),
                Size        = new Size(780, 560)
            };
            _statusBar = new Panel {
                Anchor    = AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,
                BackColor = C.Panel,
                Height    = 36,
                Location  = new Point(12, 590)
            };
            _statusLabel = new Label {
                Font      = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = C.TextDim,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10,0,0,0),
                Text      = "Initialising..."
            };
            _statusBar.Controls.Add(_statusLabel);

            _btnRun = new FlatButton { Text="Re-run",       Size=new Size(90,30), Anchor=AnchorStyles.Bottom|AnchorStyles.Right };
            _btnCopy= new FlatButton { Text="Copy Results", Size=new Size(110,30),Anchor=AnchorStyles.Bottom|AnchorStyles.Right };
            _btnClose=new FlatButton { Text="Close",        Size=new Size(80,30), Anchor=AnchorStyles.Bottom|AnchorStyles.Left  };

            _btnRun.Click  += delegate { RunDiagnostics(); };
            _btnClose.Click+= delegate { Close(); };
            _btnCopy.Click += delegate {
                if (_plainLog.Length > 0) Clipboard.SetText(_plainLog.ToString());
                _btnCopy.Text = "Copied!";
                var t = new System.Windows.Forms.Timer { Interval=1800 };
                t.Tick += delegate { _btnCopy.Text="Copy Results"; t.Stop(); };
                t.Start();
            };

            Controls.AddRange(new Control[]{ _rtb, _statusBar, _btnRun, _btnCopy, _btnClose });
            LayoutControls();
            SizeChanged += delegate { LayoutControls(); };
        }

        void LayoutControls()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            _rtb.Width  = w - 24;
            _rtb.Height = h - 108;
            _statusBar.Top   = _rtb.Bottom + 6;
            _statusBar.Width = _rtb.Width;
            int btnY = _statusBar.Bottom + 8;
            _btnClose.Location = new Point(12, btnY);
            _btnRun.Location   = new Point(w - _btnRun.Width - 14, btnY);
            _btnCopy.Location  = new Point(_btnRun.Left - _btnCopy.Width - 6, btnY);
        }

        void RunDiagnostics()
        {
            if (_worker != null && _worker.IsAlive) return;
            _btnRun.Enabled = false;
            SafeInvoke(delegate { _rtb.Clear(); _plainLog.Clear(); });
            _worker = new Thread(delegate() {
                try   { Collect(); }
                catch (Exception ex) { AppendLine("FATAL ERROR: " + ex.Message, C.Red); }
                finally { SafeInvoke(delegate { _btnRun.Enabled=true; }); }
            }) { IsBackground = true };
            _worker.Start();
        }

        void Collect()
        {
            DateTime ts       = DateTime.Now;
            string   hostname = Dns.GetHostName();

            Header("MSP NETWORK DIAGNOSTICS");

            // ---- INTERFACES ----
            Section("NETWORK INTERFACES");
            List<string> gateways = new List<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                UnicastIPAddressInformation ip4 = null;
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) { ip4=ua; break; }
                if (ip4 == null) continue;
                GatewayIPAddressInformation gw = null;
                foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses)
                    if (g.Address.AddressFamily == AddressFamily.InterNetwork) { gw=g; break; }
                AppendLine("  [" + ni.NetworkInterfaceType + "]  " + ni.Name, C.Accent);
                AppendLine("    IP      : " + ip4.Address + "  /" + MaskLen(ip4.IPv4Mask), C.TextMain);
                if (gw != null) {
                    AppendLine("    Gateway : " + gw.Address, C.TextMain);
                    if (!gateways.Contains(gw.Address.ToString())) gateways.Add(gw.Address.ToString());
                }
                AppendLine("    MAC     : " + FormatMac(ni.GetPhysicalAddress().ToString()), C.TextDim);
            }
            AppendLine("");

            // ---- PING ----
            Section("PING TESTS");
            List<string> pingTargets = new List<string>(gateways);
            pingTargets.Add("1.1.1.1");
            pingTargets.Add("gateway.codeblue.cloud");
            List<PingResult> pingResults = new List<PingResult>();
            foreach (string t in pingTargets)
                pingResults.Add(DoPing(t, 4));
            AppendLine("");

            // ---- DNS ----
            Section("DNS RESOLUTION");
            string[][] dnsTargets = new string[][] {
                new string[]{ "Local hostname",         hostname },
                new string[]{ "google.com",             "google.com" },
                new string[]{ "cloudflare.com",         "cloudflare.com" },
                new string[]{ "gateway.codeblue.cloud", "gateway.codeblue.cloud" }
            };
            List<DnsResult> dnsResults = new List<DnsResult>();
            foreach (string[] entry in dnsTargets)
            {
                string label=entry[0], host=entry[1];
                Stopwatch sw = Stopwatch.StartNew();
                try {
                    IPAddress[] addrs = Dns.GetHostAddresses(host);
                    sw.Stop();
                    string ip = "";
                    foreach (IPAddress a in addrs)
                        if (a.AddressFamily == AddressFamily.InterNetwork) { ip=a.ToString(); break; }
                    if (ip=="" && addrs.Length>0) ip=addrs[0].ToString();
                    dnsResults.Add(new DnsResult{Label=label,Ok=true,Result=ip,Ms=sw.ElapsedMilliseconds});
                    Append("  " + PadR(label,32) + " ", C.TextDim);
                    AppendLine(PadR(ip,20) + "  " + sw.ElapsedMilliseconds + " ms", C.Green);
                } catch (Exception ex) {
                    sw.Stop();
                    dnsResults.Add(new DnsResult{Label=label,Ok=false,Result=ex.Message,Ms=sw.ElapsedMilliseconds});
                    Append("  " + PadR(label,32) + " ", C.TextDim);
                    AppendLine("FAILED  (" + ex.Message + ")", C.Red);
                }
            }
            AppendLine("");

            // ---- WAN ----
            Section("WAN / EXTERNAL");
            string wanIp = "unknown";
            try {
                // Force IPv4 by resolving ifconfig.me to an A record first
                IPAddress[] ifcAddrs = Dns.GetHostAddresses("ifconfig.me");
                string ifcIp4 = "";
                foreach (IPAddress a in ifcAddrs)
                    if (a.AddressFamily == AddressFamily.InterNetwork) { ifcIp4=a.ToString(); break; }

                string url = (ifcIp4 != "") ? "http://" + ifcIp4 + "/ip" : "http://ifconfig.me/ip";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout=8000; req.UserAgent="curl/7.88 MSP-Diag/1.0";
                req.Host = "ifconfig.me";   // keep correct Host header
                WebResponse resp = req.GetResponse();
                wanIp = new StreamReader(resp.GetResponseStream()).ReadToEnd().Trim();
                resp.Close();

                // Validate it looks like an IPv4
                IPAddress parsed;
                bool isV4 = IPAddress.TryParse(wanIp, out parsed) &&
                            parsed.AddressFamily == AddressFamily.InterNetwork;

                Append("  WAN IP (IPv4) : ", C.TextDim);
                AppendLine(wanIp, isV4 ? C.Accent : C.Yellow);

                Stopwatch sw2 = Stopwatch.StartNew();
                try {
                    HttpWebRequest r2 = (HttpWebRequest)WebRequest.Create("https://1.1.1.1");
                    r2.Timeout=8000; r2.GetResponse().Close();
                } catch { }
                sw2.Stop();
                Append("  HTTP GET (1.1.1.1): ", C.TextDim);
                AppendLine(sw2.ElapsedMilliseconds + " ms", RangeColor(sw2.ElapsedMilliseconds,200,500));
            } catch (Exception ex) {
                AppendLine("  WAN check failed: " + ex.Message, C.Red);
            }
            AppendLine("");

            // ---- NET THROUGHPUT ----
            Section("INTERFACE THROUGHPUT  (1-second sample)");
            SampleNetworkAndPrint();
            AppendLine("");

            // ---- DISK ----
            Section("DISK ACTIVITY  (1-second sample)");
            foreach (DiskRow d in SampleDisks())
            {
                Append("  " + PadR(d.Drive, 6) + "  ", C.TextDim);
                Append("Read: ", C.TextDim);  AppendInline(d.ReadMBs.ToString("0.00") + " MB/s", RangeColor((long)d.ReadMBs,50,150));
                Append("   Write: ", C.TextDim); AppendInline(d.WriteMBs.ToString("0.00") + " MB/s", RangeColor((long)d.WriteMBs,50,150));
                AppendLine("   (" + d.FreeGB.ToString("0.0") + " GB free)", C.TextDim);
            }
            AppendLine("");

            // ---- CPU ----
            Section("CPU USAGE  (1-second sample)");
            float cpu = SampleCpu();
            Append("  Overall CPU: ", C.TextDim);
            if (cpu < 0) AppendLine("N/A", C.TextDim);
            else AppendLine(cpu.ToString("0.0") + " %", RangeColor((long)cpu,70,90));
            AppendLine("");

            // ---- STATUS ----
            List<string> issues=new List<string>(), warnings=new List<string>();
            foreach (PingResult pr in pingResults) {
                if (!pr.Ok)          issues.Add("Ping failed: " + pr.Host);
                else if (pr.Ms>200)  warnings.Add("High latency to " + pr.Host + ": " + pr.Ms + "ms");
            }
            foreach (DnsResult dr in dnsResults)
                if (!dr.Ok) issues.Add("DNS failed: " + dr.Label);
            if (cpu>90)      issues.Add("CPU critical: " + cpu.ToString("0.0") + "%");
            else if (cpu>70) warnings.Add("CPU elevated: " + cpu.ToString("0.0") + "%");
            if (wanIp=="unknown") issues.Add("WAN / Internet unreachable");

            string statusText; Color statusColor;
            if (issues.Count>0)        { statusText="  PROBLEM  -  " + issues[0];   statusColor=C.Red;    }
            else if (warnings.Count>0) { statusText="  WARNING  -  " + warnings[0]; statusColor=C.Yellow; }
            else                       { statusText="  ALL OK  -  No issues detected"; statusColor=C.Green; }

            AppendLine(new string('-',70), C.Border);
            AppendLine(statusText, statusColor);
            AppendLine("");

            string ft=statusText; Color fc=statusColor;
            SafeInvoke(delegate { _statusLabel.ForeColor=fc; _statusLabel.Text=ft.Trim(); });
        }

        void SampleNetworkAndPrint()
        {
            NetworkInterface best = null; long bestBytes = -1;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (ni.OperationalStatus!=OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType==NetworkInterfaceType.Loopback) continue;
                IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                long total = st.BytesSent+st.BytesReceived;
                if (total>bestBytes) { bestBytes=total; best=ni; }
            }
            if (best==null) { AppendLine("  No active interface found.", C.TextDim); return; }
            long s1=best.GetIPv4Statistics().BytesSent, r1=best.GetIPv4Statistics().BytesReceived;
            Thread.Sleep(1000);
            long s2=best.GetIPv4Statistics().BytesSent, r2=best.GetIPv4Statistics().BytesReceived;
            float up=(s2-s1)/1048576f, down=(r2-r1)/1048576f;
            Append("  " + PadR(best.Name,30) + " ", C.TextDim);
            Append("UP: ",   C.TextDim); AppendInline(up.ToString("0.000")   + " MB/s", C.TextMain);
            Append("   DOWN: ",C.TextDim); AppendInline(down.ToString("0.000") + " MB/s", C.TextMain);
            AppendLine("", null);
        }

        List<DiskRow> SampleDisks()
        {
            List<DiskRow> rows = new List<DiskRow>();
            Dictionary<string,PerformanceCounter> rpc=new Dictionary<string,PerformanceCounter>();
            Dictionary<string,PerformanceCounter> wpc=new Dictionary<string,PerformanceCounter>();
            List<DriveInfo> drives = new List<DriveInfo>();
            foreach (DriveInfo d in DriveInfo.GetDrives())
                if (d.IsReady && d.DriveType==DriveType.Fixed) drives.Add(d);
            foreach (DriveInfo d in drives) {
                string letter = d.Name.TrimEnd('\\','/');
                string inst   = "0 " + letter;
                try {
                    PerformanceCounter rc=new PerformanceCounter("PhysicalDisk","Disk Read Bytes/sec", inst);
                    PerformanceCounter wc=new PerformanceCounter("PhysicalDisk","Disk Write Bytes/sec",inst);
                    rc.NextValue(); wc.NextValue();
                    rpc[letter]=rc; wpc[letter]=wc;
                } catch { }
            }
            Thread.Sleep(1000);
            foreach (DriveInfo d in drives) {
                string letter=d.Name.TrimEnd('\\','/');
                float r=0f,w=0f;
                if (rpc.ContainsKey(letter)) try{r=rpc[letter].NextValue()/1048576f;}catch{}
                if (wpc.ContainsKey(letter)) try{w=wpc[letter].NextValue()/1048576f;}catch{}
                rows.Add(new DiskRow{Drive=d.Name,ReadMBs=r,WriteMBs=w,FreeGB=d.AvailableFreeSpace/1073741824f});
            }
            return rows;
        }

        float SampleCpu()
        {
            try {
                PerformanceCounter pc=new PerformanceCounter("Processor","% Processor Time","_Total");
                pc.NextValue(); Thread.Sleep(1000); return pc.NextValue();
            } catch { return -1f; }
        }

        PingResult DoPing(string host, int count)
        {
            List<long> times = new List<long>();
            for (int i=0; i<count; i++) {
                try {
                    using (Ping p = new Ping()) {
                        PingReply r = p.Send(host, 2000);
                        if (r.Status==IPStatus.Success) times.Add(r.RoundtripTime);
                    }
                } catch { }
                Thread.Sleep(80);
            }
            Append("  " + PadR(host,36) + " ", C.TextDim);
            if (times.Count==0) { AppendLine("UNREACHABLE", C.Red); return new PingResult{Host=host,Ok=false,Ms=-1}; }
            long avg=(long)times.Average();
            AppendLine(avg + " ms  (avg " + times.Count + "/" + count + ")", RangeColor(avg,80,200));
            return new PingResult{Host=host,Ok=true,Ms=avg};
        }

        void Header(string text)
        {
            AppendLine("  " + text + "  |  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  |  " + Dns.GetHostName(), C.Accent);
            AppendLine(new string('=',70), C.Border);
            AppendLine("");
        }

        void Section(string text)
        {
            AppendLine(">> " + text, C.Accent);
            AppendLine(new string('-',68), C.Border);
        }

        void AppendLine(string text, Color? col=null) { Append(text+"\r\n", col); }

        void Append(string text, Color? col=null)
        {
            _plainLog.Append(text);
            Color c = col.HasValue ? col.Value : C.TextMain;
            SafeInvoke(delegate {
                _rtb.SelectionStart=_rtb.TextLength; _rtb.SelectionLength=0;
                _rtb.SelectionColor=c; _rtb.AppendText(text); _rtb.ScrollToCaret();
            });
        }

        void AppendInline(string text, Color col)
        {
            _plainLog.Append(text);
            SafeInvoke(delegate {
                _rtb.SelectionStart=_rtb.TextLength; _rtb.SelectionLength=0;
                _rtb.SelectionColor=col; _rtb.AppendText(text);
            });
        }

        Color RangeColor(long val, long warn, long bad) {
            if (val>=bad)  return C.Red;
            if (val>=warn) return C.Yellow;
            return C.Green;
        }

        string PadR(string s, int len) {
            if (s==null) s="";
            if (s.Length>=len) return s.Substring(0,len);
            return s + new string(' ',len-s.Length);
        }

        int MaskLen(IPAddress mask) {
            if (mask==null) return 0;
            int bits=0;
            foreach (byte b in mask.GetAddressBytes()) { int x=b; while(x!=0){bits+=x&1;x>>=1;} }
            return bits;
        }

        string FormatMac(string raw) {
            if (raw==null||raw.Length<12) return raw??"";
            string s=raw.ToUpper();
            StringBuilder sb=new StringBuilder();
            for (int i=0;i<s.Length;i+=2){if(i>0)sb.Append('-');sb.Append(s.Substring(i,2));}
            return sb.ToString();
        }

        void SafeInvoke(Action a) {
            if (IsDisposed) return;
            try { if (InvokeRequired) Invoke(a); else a(); } catch {}
        }
    }
}
