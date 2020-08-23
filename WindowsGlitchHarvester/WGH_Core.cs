﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;
using WindowsGlitchHarvester;
using Newtonsoft.Json;
using System.Threading;

namespace WindowsGlitchHarvester
{

    public static class WGH_Core
    {
		public static string WghVersion = "0.99 RC1";

        private static volatile int _seed = DateTime.Now.Millisecond;
        public static int seed { get { return ++_seed; } }

        
        [ThreadStatic]
        private static Random _RND = null;
        public static Random RND
        {
            get
            {
                if (_RND == null)
                    _RND = new Random(seed);

                return _RND;
            }
        }
        

        //Values
        public static bool isLoaded = false;

        public static CorruptionEngine selectedEngine = CorruptionEngine.NIGHTMARE;

        public static int Intensity = 100;
        public static int StartingAddress = 0;
        public static int BlastRange = 0;
        public static bool useBlastRange = false;
        public static string ProcessHookName = "";

        public static bool ExtractBlastLayer = false;
        public static string lastOpenTarget = null;

        //General Values
        public static string currentDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
        public static string currentTargetType = "";
        public static string currentTargetName = "";
        public static string currentTargetFullName = "";
        public static string currentTargetId = "";
        public static string currentDolphinSavestate= "";

        public static bool writeCopyMode = false;
        public static bool AutoUncorrupt = true;

        //Forms
        public static WGH_MainForm ghForm;
        public static WGH_SelectMultipleForm smForm = null;
		//public static WGH_HookProcessForm hpForm = null;
        public static WGH_BlastEditorForm beForm = null;
        public static WGH_Progress progressForm = null;

        //object references
        public static MemoryInterface currentMemoryInterface = null;
        public static Stockpile currentStockpile = null;
        public static StashKey currentStashkey = null;
        public static BlastLayer lastBlastLayerBackup = null;

        //File management
        public static Dictionary<String, String> CompositeFilenameDico = null;

        public static int ErrorDelay = 100;

		public static void Start(WGH_MainForm _ghForm)
        {

            bool Expires = false;
            DateTime ExpiringDate = DateTime.Parse("2015-01-02");

            if (Expires && DateTime.Now > ExpiringDate)
            {
                MessageBox.Show("This version has expired");
                Application.Exit();
                return;
            }

            ghForm = _ghForm;

            if (!Directory.Exists(WGH_Core.currentDir + "\\TEMP\\"))
                Directory.CreateDirectory(WGH_Core.currentDir + "\\TEMP\\");

            if (!Directory.Exists(WGH_Core.currentDir + "\\TEMP2\\"))
                Directory.CreateDirectory(WGH_Core.currentDir + "\\TEMP2\\");

            if (!Directory.Exists(WGH_Core.currentDir + "\\PARAMS\\"))
                Directory.CreateDirectory(WGH_Core.currentDir + "\\PARAMS\\");


            if (File.Exists(currentDir + "\\PARAMS\\COLOR.TXT"))
			{
				string[] bytes = File.ReadAllText(currentDir + "\\PARAMS\\COLOR.TXT").Split(',');
				SetWGHColor(Color.FromArgb(Convert.ToByte(bytes[0]), Convert.ToByte(bytes[1]), Convert.ToByte(bytes[2])));
			}
			else
				SetWGHColor(Color.FromArgb(150, 150, 180));

            if (File.Exists(currentDir + "\\LICENSES\\DISCLAIMER.TXT") && !File.Exists(currentDir + "\\PARAMS\\DISCLAIMERREAD"))
            {
                MessageBox.Show(File.ReadAllText(currentDir + "\\LICENSES\\DISCLAIMER.TXT").Replace("[ver]", WGH_Core.WghVersion), "WGH", MessageBoxButtons.OK, MessageBoxIcon.Information);
                File.Create(currentDir + "\\PARAMS\\DISCLAIMERREAD");
            }

            //If we can't load the dictionary, quit the wgh to prevent the loss of backups
            if (!LoadCompositeFilenameDico())
                Application.Exit();

        }

        public static void FormExecute(Action<object> action, object[] args = null) => FormExecute(ghForm, action, args);
        public static void FormExecute(Form f, Action<object> a, object[] args = null)
        {
            if (f.InvokeRequired)
                f.Invoke(new MethodInvoker(() => { a.Invoke(null); }));
            else
                a.Invoke(null);
        }


        public static bool LoadCompositeFilenameDico()
        {
            JsonSerializer serializer = new JsonSerializer();
            var path = WGH_Core.currentDir + "\\TEMP\\" + "filemap.json";
            if (!File.Exists(path))
            {
                CompositeFilenameDico = new Dictionary<string, string>();
                return true;
            }
            try
            {

                using (StreamReader sw = new StreamReader(WGH_Core.currentDir + "\\TEMP\\" + "filemap.json"))
                using (JsonTextReader reader = new JsonTextReader(sw))
                {
                    CompositeFilenameDico = serializer.Deserialize<Dictionary<string, string>>(reader);
                }
            }
            catch (IOException e)
            {
                MessageBox.Show("Unable to access the filemap! Figure out what's locking it and then restart the WGH.\n" + e.ToString());
                return false;
            }
              return true;
        }

        public static bool SaveCompositeFilenameDico()
        {
            JsonSerializer serializer = new JsonSerializer();
            var path = WGH_Core.currentDir + "\\TEMP\\" + "filemap.json";
            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, CompositeFilenameDico);
                }
            }
            catch(IOException e)
            {
                MessageBox.Show("Unable to access the filemap!\n" + e.ToString());
                return false;
            }
            return true;
        }

        public static long RandomLong(long max)
        {
            return RND.RandomLong(0, max);
        }


		public static BlastUnit getBlastUnit(string _target, long _address)
        {

            BlastUnit bu = null;

            switch (selectedEngine)
            {
                case CorruptionEngine.NIGHTMARE:
                    bu = WGH_NightmareEngine.GenerateUnit(_target, _address);
                    break;
                case CorruptionEngine.VECTOR:
                    bu = WGH_VectorEngine.GenerateUnit(_target, _address);
                    break;

                case CorruptionEngine.NONE:
                    return null;
            }

            return bu;
        }

        //Generates or queries a blast layer then applies it.
        public static BlastLayer Blast(BlastLayer _layer, int requestedIntensity = 0)
        {
            if (requestedIntensity == 0)
                requestedIntensity = Intensity;

            try
            {
                if (_layer != null)
                {
                    _layer.Apply(); //If the BlastLayer was provided, there's no need to generate a new one.

                    currentMemoryInterface.ApplyWorkingFile();

                    return _layer;
                }
                else
                {
                    BlastLayer bl = new BlastLayer();

                    string TargetType;
                    long StartingAddress;
                    long BlastRange;
                    long MaxAddress;
                    long RandomAdress = 0;
                    BlastUnit bu;

                    TargetType = currentTargetType;
                    StartingAddress = WGH_Core.StartingAddress;
                    BlastRange = WGH_Core.BlastRange;
                    MaxAddress = (long)(currentMemoryInterface.lastMemorySize ?? 0 );

                    if (!WGH_Core.useBlastRange)
                        BlastRange = MaxAddress - StartingAddress;
                    else if (StartingAddress + BlastRange > MaxAddress)
                        BlastRange = MaxAddress - StartingAddress;


                    int reportCounter = 0;
                    for (int i = 0; i < requestedIntensity; i++)
                    {
                        RandomAdress = StartingAddress + RandomLong(BlastRange -1);

                        bu = getBlastUnit(TargetType, RandomAdress);
                        if (bu != null)
                            bl.Layer.Add(bu);

                        reportCounter++;
                        if (reportCounter > 1000)
                        {
                            progressForm?.bw?.ReportProgress(1, "DEFAULT");
                            reportCounter = 0;
                        }
                    }

                        return bl;

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong in the WGH Core. \n\n" +
                ex.ToString());
                return null;
            }
        }

        public static BlastLayer Blast()
        {
            bool multithread = WGH_Core.currentMemoryInterface.cacheEnabled;
            var cpus = Environment.ProcessorCount;
            int splitintensity = Intensity / cpus;

            BlastLayer bl = new BlastLayer();

            List<BlastLayer> returns = new List<BlastLayer>();
            object returnLock = new object();

            if (multithread)
            {

                Thread[] threads = new Thread[cpus];
                for (int i = 0; i < cpus; i++)
                {
                    ParameterizedThreadStart pts = new ParameterizedThreadStart((o) =>
                    {
                        lock (returnLock)
                        {
                            returns.Add(Blast(null, splitintensity));
                        }
                    });
                    threads[i] = new Thread(pts);
                }

                while(true)
                {
                    bool allDone = true;
                    for (int i = 0; i < cpus; i++)
                        if(threads[i].IsAlive)
                            allDone = false;

                    if (allDone)
                        break;

                    Thread.Sleep(69);
                }


                    /*
                        Task<BlastLayer>[] tasks = new Task<BlastLayer>[cpus];
                    for (int i = 0; i < cpus; i++)
                        tasks[i] = Task.Factory.StartNew(() => Blast(null, splitintensity));

                    Task.WaitAll(tasks);
                    */

                bl = returns[0];

                if (returns.Count > 1)
                    for (int i = 1; i < returns.Count; i++)
                        bl.Layer.AddRange(returns[i].Layer);

            }
            else
                bl = Blast(null);
            
            progressForm?.bw?.ReportProgress(0, "Applying units...");
            bl.Apply();

            currentMemoryInterface.ApplyWorkingFile();

            if (bl.Layer.Count == 0)
                return null;
            else
                return bl;


        }

        public static string GetRandomKey()
        {

            string Key = RND.Next(1, 9999).ToString() + RND.Next(1, 9999).ToString() + RND.Next(1, 9999).ToString() + RND.Next(1, 9999).ToString();
            return Key;
        }

        public static void LoadTarget()
        {
            if (WGH_Core.ghForm.rbTargetFile.Checked)
            {
                OpenFileDialog OpenFileDialog1;
                OpenFileDialog1 = new OpenFileDialog();

                OpenFileDialog1.Title = "Open File";
                OpenFileDialog1.Filter = "files|*.*";
                OpenFileDialog1.RestoreDirectory = true;
                if (OpenFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (OpenFileDialog1.FileName.ToString().Contains("^"))
                    {
                        MessageBox.Show("You can't use a file that contains the character ^ ");
                        return;
                    }

                    currentTargetId = "File|" + OpenFileDialog1.FileName.ToString();
                    currentTargetFullName = OpenFileDialog1.FileName.ToString();
                }
                else
                    return;

                //Disable caching of the previously loaded file if it was enabled
                if (ghForm.btnEnableCaching.Text.Contains("Disable"))
                    ghForm.btnEnableCaching.PerformClick();

                if (currentMemoryInterface != null && (currentTargetType == "Dolphin" || currentTargetType == "File" || currentTargetType == "MultipleFiles"))
                {
                    WGH_Core.RestoreTarget();
                    currentMemoryInterface.stream?.Dispose();
                }

                currentTargetType = "File";

                FileInterface fi = null;

                Action<object> action = (ob) =>
                {
                    fi = new FileInterface(currentTargetId);
                };

                Action<object> postAction = (ob) =>
                {
                    if (fi == null || fi.lastMemorySize == null)
                    {
                        MessageBox.Show("Failed to load target");
                        return;
                    }

                    currentTargetName = fi.ShortFilename;

                    currentMemoryInterface = fi;
                    ghForm.lbTarget.Text = currentTargetId + "|MemorySize:" + fi.lastMemorySize.ToString();

                    //Refresh the UI
                    RefreshUIPostLoad();
                };

                WGH_Core.ghForm.RunProgressBar($"Loading target...", 0, action, postAction);
                return;
            }
            else if (WGH_Core.ghForm.rbTargetMultipleFiles.Checked)
            {
                if (smForm != null)
                    smForm.Close();

                smForm = new WGH_SelectMultipleForm();

                if (smForm.ShowDialog() != DialogResult.OK)
                {
                    WGH_Core.currentMemoryInterface = null;
                    return;
                }

                currentTargetType = "MultipleFiles";
                var mfi = (MultipleFileInterface)WGH_Core.currentMemoryInterface;
                currentTargetName = mfi.ShortFilename;
                ghForm.lbTarget.Text = mfi.ShortFilename + "|MemorySize:" + mfi.lastMemorySize.ToString();
            }
            /*
            else if (WGH_Core.ghForm.rbTargetProcess.Checked)
            {
                if (hpForm != null)
                    hpForm.Close();

                hpForm = new WGH_HookProcessForm();

                if (hpForm.ShowDialog() != DialogResult.OK)
                {
                    WGH_Core.currentMemoryInterface = null;
                    return;
                }

                currentTargetType = "Process";
                var mfi = (ProcessInterface)WGH_Core.currentMemoryInterface;
                currentTargetName = mfi.ProcessName;
                ghForm.lbTarget.Text = mfi.ProcessName + "|MemorySize:" + mfi.lastMemorySize.ToString();
            }
            */
            /*
            else if (WGH_Core.ghForm.rbTargetDolphin.Checked)
            {

                currentTargetType = "Dolphin";
                //var mfi = (DolphinInterface)WGH_Core.currentMemoryInterface;
                WGH_Core.currentMemoryInterface = new DolphinInterface("Test");
                currentTargetName = "Dolphin";
                ghForm.lbTarget.Text = currentTargetName + "|MemorySize:" + WGH_Core.currentMemoryInterface.lastMemorySize.ToString();
            }*/

            //Refresh the UI
            RefreshUIPostLoad();
        }

        public static void RefreshUIPostLoad()
        {

            if (WGH_Core.currentMemoryInterface != null)
            {
                var mi = WGH_Core.currentMemoryInterface;

                if (mi.lastMemorySize != null)
                {
                    if (mi.lastMemorySize >= int.MaxValue)
                    {
                        WGH_Core.ghForm.mtb_StartingAddress.Visible = false;
                        WGH_Core.ghForm.mtb_BlastRange.Visible = false;
                    }
                    else
                    {
                        WGH_Core.ghForm.mtb_StartingAddress.Visible = true;
                        WGH_Core.ghForm.mtb_BlastRange.Visible = true;
                    }


                    if (WGH_Core.ghForm.mtb_StartingAddress.Visible && WGH_Core.ghForm.mtb_StartingAddress.Value > mi.lastMemorySize && WGH_Core.ghForm.mtb_StartingAddress.Maximum > mi.lastMemorySize)
                        WGH_Core.ghForm.mtb_StartingAddress.Value = (int)mi.lastMemorySize;

                    WGH_Core.ghForm.mtb_StartingAddress.Maximum = (long)mi.lastMemorySize;

                    if (WGH_Core.ghForm.mtb_BlastRange.Visible && WGH_Core.ghForm.mtb_BlastRange.Value > mi.lastMemorySize && WGH_Core.ghForm.mtb_BlastRange.Maximum > mi.lastMemorySize)
                        WGH_Core.ghForm.mtb_BlastRange.Value = (int)mi.lastMemorySize;

                    WGH_Core.ghForm.mtb_BlastRange.Maximum = (long)mi.lastMemorySize;

                    if (WGH_Core.ghForm.mtb_StartingAddress.Visible)
                    {
                        WGH_Core.ghForm.mtb_StartingAddress.Maximum = (int)mi.lastMemorySize;
                        WGH_Core.ghForm.mtb_BlastRange.Maximum = (int)mi.lastMemorySize;
                    }

                    WGH_Core.lastBlastLayerBackup = new BlastLayer();
                }
            }
        }

        public static void RestoreTarget()
        {
            if (WGH_Core.AutoUncorrupt)
            {
                if (WGH_Core.lastBlastLayerBackup != null)
                    WGH_Core.lastBlastLayerBackup.Apply();
                else
                {
                    //CHECK CRC WITH BACKUP HERE AND SKIP BACKUP IF WORKING FILE = BACKUP FILE
                    WGH_Core.currentMemoryInterface.ResetWorkingFile();
                }
            }
            else
            {
                WGH_Core.currentMemoryInterface.ResetWorkingFile();
            }
        }


		/// <summary>
		/// Creates color with corrected brightness.
		/// </summary>
		/// <param name="color">Color to correct.</param>
		/// <param name="correctionFactor">The brightness correction factor. Must be between -1 and 1. 
		/// Negative values produce darker colors.</param>
		/// <returns>
		/// Corrected <see cref="Color"/> structure.
		/// </returns>
		public static Color ChangeColorBrightness(Color color, float correctionFactor)
		{
			float red = (float)color.R;
			float green = (float)color.G;
			float blue = (float)color.B;

			if (correctionFactor < 0)
			{
				correctionFactor = 1 + correctionFactor;
				red *= correctionFactor;
				green *= correctionFactor;
				blue *= correctionFactor;
			}
			else
			{
				red = (255 - red) * correctionFactor + red;
				green = (255 - green) * correctionFactor + green;
				blue = (255 - blue) * correctionFactor + blue;
			}

			return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
		}

		private static List<Control> FindTag(Control.ControlCollection controls)
		{
			List<Control> allControls = new List<Control>();

			foreach (Control c in controls)
			{
				if (c.Tag != null)
					allControls.Add(c);

				if (c.HasChildren)
					allControls.AddRange(FindTag(c.Controls)); //Recursively check all children controls as well; ie groupboxes or tabpages
			}

			return allControls;
		}

		public static void SetWGHColor(Color color, Form form = null)
		{
			List<Control> allControls = new List<Control>();

			if (form == null)
			{
				if (ghForm != null)
				{
					allControls.AddRange(FindTag(ghForm.Controls));
					allControls.Add(ghForm);
				}
				

			}
			else
				allControls.AddRange(FindTag(form.Controls));

			var lightColorControls = allControls.FindAll(it => (it.Tag as string).Contains("color:light"));
			var normalColorControls = allControls.FindAll(it => (it.Tag as string).Contains("color:normal"));
			var darkColorControls = allControls.FindAll(it => (it.Tag as string).Contains("color:dark"));
			var darkerColorControls = allControls.FindAll(it => (it.Tag as string).Contains("color:darker"));
            var darkestColorControls = allControls.FindAll(it => (it.Tag as string).Contains("color:darkest"));

            foreach (Control c in lightColorControls)
				c.BackColor = ChangeColorBrightness(color, 0.30f);

			foreach (Control c in normalColorControls)
				c.BackColor = color;

			//spForm.dgvStockpile.BackgroundColor = color;
			//ghForm.dgvStockpile.BackgroundColor = color;

			foreach (Control c in darkColorControls)
				c.BackColor = ChangeColorBrightness(color, -0.550f);

			foreach (Control c in darkerColorControls)
				c.BackColor = ChangeColorBrightness(color, -0.70f);

            foreach (Control c in darkestColorControls)
                c.BackColor = ChangeColorBrightness(color, -0.80f);

        }

		public static void SetAndSaveColorWGH()
		{
			// Show the color dialog.
			Color color;
			ColorDialog cd = new ColorDialog();
			DialogResult result = cd.ShowDialog();
			// See if user pressed ok.
			if (result == DialogResult.OK)
			{
				// Set form background to the selected color.
				color = cd.Color;
			}
			else
				return;

			SetWGHColor(color);

			if (File.Exists(currentDir + "\\PARAMS\\COLOR.TXT"))
				File.Delete(currentDir + "\\PARAMS\\COLOR.TXT");

			File.WriteAllText(currentDir + "\\PARAMS\\COLOR.TXT", color.R.ToString() + "," + color.G.ToString() + "," + color.B.ToString());
		}

	}

	static class RandomExtensions
	{
		public static long RandomLong(this Random rnd)
		{
			byte[] buffer = new byte[8];
			rnd.NextBytes(buffer);
			return BitConverter.ToInt64(buffer, 0);
		}

		public static long RandomLong(this Random rnd, long min, long max)
		{
			EnsureMinLEQMax(ref min, ref max);
			long numbersInRange = unchecked(max - min + 1);
			if (numbersInRange < 0)
				throw new ArgumentException("Size of range between min and max must be less than or equal to Int64.MaxValue");

			long randomOffset = RandomLong(rnd);
			if (IsModuloBiased(randomOffset, numbersInRange))
				return RandomLong(rnd, min, max); // Try again
			else
				return min + PositiveModuloOrZero(randomOffset, numbersInRange);
		}

		static bool IsModuloBiased(long randomOffset, long numbersInRange)
		{
			long greatestCompleteRange = numbersInRange * (long.MaxValue / numbersInRange);
			return randomOffset > greatestCompleteRange;
		}

		static long PositiveModuloOrZero(long dividend, long divisor)
		{
			long mod;
			Math.DivRem(dividend, divisor, out mod);
			if (mod < 0)
				mod += divisor;
			return mod;
		}

		static void EnsureMinLEQMax(ref long min, ref long max)
		{
			if (min <= max)
				return;
			long temp = min;
			min = max;
			max = temp;
		}
	}
}
