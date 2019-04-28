using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using NAudio.Wave;

namespace PTTRecorderWpf {
	public partial class MainWindow : Window {
		readonly WaveInEvent WaveIn = new WaveInEvent { WaveFormat = new WaveFormat(44100, 16, 1) };
		WaveFileWriter WaveOut;
		bool Recording, Replaying, DoDelete;
		int Inc;
		string LastPath;
		readonly Stack<string> Filenames = new Stack<string>();

		public MainWindow() {
			InitializeComponent();

			PTT.PreviewMouseDown += async (sender, args) => await Record();
			PTT.PreviewMouseUp += (sender, args) => Recording = false;
			DeleteLast.Click += (sender, args) => Delete();
			Path.TextChanged += (sender, args) => {
				if(LastPath != Path.Text)
					Inc = 0;
				LastPath = Path.Text;
			};
		}

		const int RecordKey = 0x2D; // insert
		const int DeleteKey = 0x2E; // Delete
		const int ReplayKey = 0x24; // Home

		[DllImport("User32.dll")]
		static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);
		[DllImport("User32.dll")]
		static extern ushort GetAsyncKeyState(int vk);
		
		protected override void OnSourceInitialized(EventArgs e) {
			var helper = new WindowInteropHelper(this);
			RegisterHotKey(helper.Handle, 9000, 0, RecordKey);
			RegisterHotKey(helper.Handle, 9001, 0, DeleteKey);
			RegisterHotKey(helper.Handle, 9002, 0, ReplayKey);
			HwndSource.FromHwnd(helper.Handle)?.AddHook((IntPtr hwnd, int msg, IntPtr param, IntPtr lParam, ref bool handled) => {
				if(msg != 0x0312) return IntPtr.Zero;
				switch(param.ToInt32()) {
					case 9000:
						new Task(async () => await Record(RecordKey)).Start();
						break;
					case 9001:
						Delete();
						break;
					case 9002:
						Replay();
						break;
				}
				return IntPtr.Zero;
			});

			WaveIn.DataAvailable += (sender, args) => WaveOut?.Write(args.Buffer, 0, args.BytesRecorded);
			WaveIn.RecordingStopped += (sender, args) => {
				WaveOut?.Dispose();
				WaveOut = null;
			};
		}

		async Task Record(int vkey = -1) {
			if(Recording || LastPath == null || LastPath == "")
				return;
			Recording = true;
			var dir = $@"c:\aaa\projects\h1recordings\{LastPath}";
			Directory.CreateDirectory(dir);
			var fn = $@"{dir}\{Inc++}0.wav";
			Filenames.Push(fn);
			WaveOut = new WaveFileWriter(fn, WaveIn.WaveFormat);
			WaveIn.StartRecording();

			while(Recording) {
				if(vkey != -1 && (GetAsyncKeyState(vkey) & 0x8000) == 0)
					break;
				await Task.Delay(50);
			}
			Recording = false;
			WaveIn.StopRecording();
		}

		void Delete() {
			if(Replaying) {
				StopReplay();
				DoDelete = true;
				return;
			}
			if(Filenames.Count != 0)
				File.Delete(Filenames.Pop());
			DoDelete = false;
		}

		WaveOutEvent Wo;

		void Replay() {
			if(Filenames.Count == 0) return;
			if(Replaying) {
				StopReplay();
				return;
			}

			Replaying = true;
			Wo = new WaveOutEvent();
			var af = new AudioFileReader(Filenames.Peek());
			Wo.PlaybackStopped += (_, __) => {
				Wo.Dispose();
				af.Dispose();
				Replaying = false;
				if(DoDelete) Delete();
			};
			
			Wo.Init(af);
			Wo.Play();
		}

		void StopReplay() =>
			Wo.Stop();
	}
}
