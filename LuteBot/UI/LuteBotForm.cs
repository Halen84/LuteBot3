﻿using Lutebot.UI;
using LuteBot.Config;
using LuteBot.Core;
using LuteBot.Core.Midi;
using LuteBot.IO.KB;
using LuteBot.LiveInput.Midi;
using LuteBot.OnlineSync;
using LuteBot.playlist;
using LuteBot.Soundboard;
using LuteBot.TrackSelection;
using LuteBot.UI;
using LuteBot.UI.Utils;
using LuteBot.Utils;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LuteBot
{
    public partial class LuteBotForm : Form
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static readonly string libraryPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\LuteBot\GuildLibrary\";

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        static HotkeyManager hotkeyManager;

        TrackSelectionForm trackSelectionForm;
        OnlineSyncForm onlineSyncForm;
        SoundBoardForm soundBoardForm;
        public PlayListForm playListForm;
        LiveInputForm liveInputForm;
        TimeSyncForm timeSyncForm = null;
        PartitionsForm partitionsForm = null;

        MidiPlayer player;


        string playButtonStartString = "Play";
        string playButtonStopString = "Pause";
        string musicNameLabelHeader = "Playing : ";
        bool playButtonIsPlaying = false;
        public string currentTrackName { get; set; } = "";
        bool autoplay = false;
        bool isDonePlaying = false;

        public static PlayListManager playList;
        static SoundBoardManager soundBoardManager;
        public static TrackSelectionManager trackSelectionManager;
        static OnlineSyncManager onlineManager;
        static LiveMidiManager liveMidiManager;

        bool closing = false;

        public LuteBotForm()
        {
            InitializeComponent();

            onlineManager = new OnlineSyncManager();
            playList = new PlayListManager();
            trackSelectionManager = new TrackSelectionManager();
            playList.PlayListUpdatedEvent += new EventHandler<PlayListEventArgs>(HandlePlayListChanged);
            soundBoardManager = new SoundBoardManager();
            soundBoardManager.SoundBoardTrackRequest += new EventHandler<SoundBoardEventArgs>(HandleSoundBoardTrackRequest);
            player = new MidiPlayer(trackSelectionManager);
            player.SongLoaded += new EventHandler<AsyncCompletedEventArgs>(PlayerLoadCompleted);
            hotkeyManager = new HotkeyManager();
            hotkeyManager.NextKeyPressed += new EventHandler(NextButton_Click);
            hotkeyManager.PlayKeyPressed += new EventHandler(PlayButton_Click);
            hotkeyManager.StopKeyPressed += new EventHandler(StopButton_Click);
            hotkeyManager.SynchronizePressed += HotkeyManager_SynchronizePressed;
            hotkeyManager.PreviousKeyPressed += new EventHandler(PreviousButton_Click);
            trackSelectionManager.OutDeviceResetRequest += new EventHandler(ResetDevice);
            trackSelectionManager.ToggleTrackRequest += new EventHandler<TrackItem>(ToggleTrack);
            liveMidiManager = new LiveMidiManager(trackSelectionManager);
            hotkeyManager.LiveInputManager = liveMidiManager;

            PlayButton.Enabled = false;
            StopButton.Enabled = false;
            PreviousButton.Enabled = false;
            NextButton.Enabled = false;
            MusicProgressBar.Enabled = false;


            _hookID = SetHook(_proc);
            OpenDialogs();
            this.StartPosition = FormStartPosition.Manual;
            Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.MainWindowPos));
            Top = coords.Y;
            Left = coords.X;

            // We may package this with a guild library for now.  Check for it and extract it, if so
            var files = Directory.GetFiles(Environment.CurrentDirectory, "BGML*.zip", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                Task.Run(() =>
                {
                    // extract to libraryPath + "\songs\"
                    try
                    {
                        ZipFile.ExtractToDirectory(files[0], libraryPath + @"\songs\");
                        //File.Delete(files[0]);
                    }
                    catch (Exception e) { } // Gross I know, but no reason to do anything
                });
            }


            // Try to catch issues with mismatches in configs
            try
            {
                int chords = ConfigManager.GetIntegerProperty(PropertyItem.NumChords);
            }
            catch
            {
                ConfigManager.SetProperty(PropertyItem.NumChords, "3");
                ConfigManager.SaveConfig();
            }
        }

        private void HotkeyManager_SynchronizePressed(object sender, EventArgs e)
        {
            if (timeSyncForm != null)
            {
                timeSyncForm.StartAtNextInterval(10);
            }
        }

        private void PlayerLoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                StopButton_Click(null, null);
                PlayButton.Enabled = true;
                MusicProgressBar.Enabled = true;
                StopButton.Enabled = true;
                
                trackSelectionManager.UnloadTracks();
                if (player.GetType() == typeof(MidiPlayer))
                {
                    MidiPlayer midiPlayer = player as MidiPlayer;
                    trackSelectionManager.LoadTracks(midiPlayer.GetMidiChannels(), midiPlayer.GetMidiTracks(), trackSelectionManager);
                    trackSelectionManager.FileName = currentTrackName;
                }

                if (trackSelectionManager.autoLoadProfile)
                {
                    trackSelectionManager.LoadTrackManager();
                }

                MusicProgressBar.Value = 0;
                MusicProgressBar.Maximum = player.GetLength();
                StartLabel.Text = TimeSpan.FromSeconds(0).ToString(@"mm\:ss");
                EndTimeLabel.Text = player.GetFormattedLength();
                CurrentMusicLabel.Text = musicNameLabelHeader + Path.GetFileNameWithoutExtension(currentTrackName);
                if (autoplay)
                {
                    Play();
                    autoplay = false;
                }
            }
            else
            {
                MessageBox.Show(e.Error.Message + " in " + e.Error.Source + e.Error.TargetSite + "\n" + e.Error.InnerException + "\n" + e.Error.StackTrace);
            }
        }

        private void ToggleTrack(object sender, TrackItem e)
        {
            timer1.Stop();
            (player as MidiPlayer).UpdateMutedTracks(e);
            timer1.Start();
        }

        private void ResetDevice(object sender, EventArgs e)
        {
            (player as MidiPlayer).ResetDevice();
        }

        private void LuteBotForm_Focus(object sender, EventArgs e)
        {
            if (trackSelectionForm != null && !trackSelectionForm.IsDisposed)
            {
                if (trackSelectionForm.WindowState == FormWindowState.Minimized)
                {
                    trackSelectionForm.WindowState = FormWindowState.Normal;
                }
                trackSelectionForm.Focus();
            }
            if (onlineSyncForm != null && !onlineSyncForm.IsDisposed)
            {
                if (onlineSyncForm.WindowState == FormWindowState.Minimized)
                {
                    onlineSyncForm.WindowState = FormWindowState.Normal;
                }
                onlineSyncForm.Focus();
            }
            if (soundBoardForm != null && !soundBoardForm.IsDisposed)
            {
                if (soundBoardForm.WindowState == FormWindowState.Minimized)
                {
                    soundBoardForm.WindowState = FormWindowState.Normal;
                }
                soundBoardForm.Focus();
            }
            if (playListForm != null && !playListForm.IsDisposed)
            {
                if (playListForm.WindowState == FormWindowState.Minimized)
                {
                    playListForm.WindowState = FormWindowState.Normal;
                }
                playListForm.Focus();
            }
            if (liveInputForm != null && !liveInputForm.IsDisposed)
            {
                if (liveInputForm.WindowState == FormWindowState.Minimized)
                {
                    liveInputForm.WindowState = FormWindowState.Normal;
                }
                liveInputForm.Focus();
            }
            this.Focus();
        }

        private void HandleSoundBoardTrackRequest(object sender, SoundBoardEventArgs e)
        {
            isDonePlaying = false;
            Pause();
            LoadHelper(e.SelectedTrack);
            autoplay = true;
        }

        private void HandlePlayListChanged(object sender, PlayListEventArgs e)
        {
            if (e.EventType == PlayListEventArgs.UpdatedComponent.UpdateNavButtons)
            {
                ToggleNavButtons(playList.HasNext());
            }
            if (e.EventType == PlayListEventArgs.UpdatedComponent.PlayRequest)
            {
                isDonePlaying = false;
                Pause();
                LoadHelper(playList.Get(e.Id));
                autoplay = true;
            }
        }

        private void ToggleNavButtons(bool enable)
        {
            PreviousButton.Enabled = enable;
            NextButton.Enabled = enable;
        }

        private void MusicProgressBar_Scroll(object sender, EventArgs e)
        {
            player.SetPosition(MusicProgressBar.Value);
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (player.GetPosition() < MusicProgressBar.Maximum)
            {
                MusicProgressBar.Value = player.GetPosition();
                StartLabel.Text = player.GetFormattedPosition();
            }
            else
            {
                if (ActionManager.AutoConsoleModeFromString(ConfigManager.GetProperty(PropertyItem.ConsoleOpenMode)) == ActionManager.AutoConsoleMode.Old)
                {
                    ActionManager.ToggleConsole(false);
                }
                StartLabel.Text = EndTimeLabel.Text;
                PlayButton.Text = playButtonStartString;
                isDonePlaying = true;
                timer1.Stop();
                if (NextButton.Enabled)
                {
                    NextButton.PerformClick();
                }
            }
        }

        private void OpenDialogs()
        {
            if (ConfigManager.GetBooleanProperty(PropertyItem.SoundBoard))
            {
                soundBoardForm = new SoundBoardForm(soundBoardManager);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.SoundBoardPos));
                soundBoardForm.Show();
                soundBoardForm.Top = coords.Y;
                soundBoardForm.Left = coords.X;
            }
            if (ConfigManager.GetBooleanProperty(PropertyItem.PlayList))
            {
                playListForm = new PlayListForm(playList);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.PlayListPos));
                playListForm.Show();
                playListForm.Top = coords.Y;
                playListForm.Left = coords.X;

            }
            if (ConfigManager.GetBooleanProperty(PropertyItem.TrackSelection))
            {
                var midiPlayer = player as MidiPlayer;
                trackSelectionForm = new TrackSelectionForm(trackSelectionManager, midiPlayer.mordhauOutDevice);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.TrackSelectionPos));
                trackSelectionForm.Show();
                trackSelectionForm.Top = coords.Y;
                trackSelectionForm.Left = coords.X;
            }
            if (ConfigManager.GetBooleanProperty(PropertyItem.LiveMidi))
            {
                liveInputForm = new LiveInputForm(liveMidiManager);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.LiveMidiPos));
                liveInputForm.Show();
                liveInputForm.Top = coords.Y;
                liveInputForm.Left = coords.X;
            }
        }

        protected override void WndProc(ref Message m)
        {
            hotkeyManager.HotkeyPressed(m.Msg);
            base.WndProc(ref m);
        }

        private void KeyBindingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new KeyBindingForm()).ShowDialog();
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new SettingsForm(player as MidiPlayer)).ShowDialog();
            player.Pause();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            player.Dispose();
            WindowPositionUtils.UpdateBounds(PropertyItem.MainWindowPos, new Point() { X = Left, Y = Top });
            if (soundBoardForm != null)
            {
                soundBoardForm.Close();
            }
            if (playListForm != null)
            {
                playListForm.Close();
            }
            if (trackSelectionForm != null)
            {
                trackSelectionForm.Close();
            }
            if (liveInputForm != null)
            {
                liveInputForm.Close();
            }
            ConfigManager.SaveConfig();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
            base.OnClosed(e);
        }

        private void LoadFileButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openMidiFileDialog = new OpenFileDialog();
            openMidiFileDialog.DefaultExt = "mid";
            openMidiFileDialog.Filter = "MIDI files|*.mid|All files|*.*";
            openMidiFileDialog.Title = "Open MIDI file";
            if (openMidiFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openMidiFileDialog.FileName;
                player.LoadFile(fileName);
                //if (fileName.Contains("\\"))
                //{
                //    string[] fileNameSplit = fileName.Split('\\');
                //    string filteredFileName = fileNameSplit[fileNameSplit.Length - 1].Replace(".mid", "");
                //    currentTrackName = filteredFileName;
                //}
                //else
                //{
                currentTrackName = fileName;
                //}
            }
        }

        private void LoadHelper(PlayListItem item)
        {
            player.LoadFile(item.Path);
            currentTrackName = item.Path;
        }

        private void LoadHelper(SoundBoardItem item)
        {
            player.LoadFile(item.Path);
            currentTrackName = item.Path;
        }

        public void LoadHelper(string path)
        {
            player.LoadFile(path);
            currentTrackName = path;
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (PlayButton.Enabled)
            {

                if (isDonePlaying)
                {
                    player.Stop();
                    player.Play();
                    playButtonIsPlaying = false;
                    isDonePlaying = false;
                }
                if (!playButtonIsPlaying)
                {
                    Play();
                }
                else
                {
                    Pause();
                }
            }
        }

        public void Play()
        {
            if (ActionManager.AutoConsoleModeFromString(ConfigManager.GetProperty(PropertyItem.ConsoleOpenMode)) == ActionManager.AutoConsoleMode.Old)
            {
                ActionManager.ToggleConsole(true);
            }
            PlayButton.Text = playButtonStopString;
            player.Play();

            timer1.Start();
            playButtonIsPlaying = true;
        }

        private void Pause()
        {
            if (ActionManager.AutoConsoleModeFromString(ConfigManager.GetProperty(PropertyItem.ConsoleOpenMode)) == ActionManager.AutoConsoleMode.Old)
            {
                ActionManager.ToggleConsole(false);
            }
            PlayButton.Text = playButtonStartString;
            player.Pause();
            timer1.Stop();
            playButtonIsPlaying = false;
        }

        private void PlayListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playListForm == null || playListForm.IsDisposed)
            {
                playListForm = new PlayListForm(playList);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.PlayListPos));
                playListForm.Show();
                playListForm.Top = coords.Y;
                playListForm.Left = coords.X;
            }


        }

        private void SoundBoardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (soundBoardForm == null || soundBoardForm.IsDisposed)
            {
                soundBoardForm = new SoundBoardForm(soundBoardManager);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.SoundBoardPos));
                soundBoardForm.Show();
                soundBoardForm.Top = coords.Y;
                soundBoardForm.Left = coords.X;
            }

        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            player.Stop();
            timer1.Stop();
            MusicProgressBar.Value = 0;
            //PlayButton.Enabled = false;
            //MusicProgressBar.Enabled = false;
            //StopButton.Enabled = false;
            StartLabel.Text = "00:00";
            //EndTimeLabel.Text = "00:00";
            //CurrentMusicLabel.Text = "";
            playButtonIsPlaying = false;
            PlayButton.Text = playButtonStartString;
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            if (NextButton.Enabled)
            {
                PlayButton.Enabled = false;
                StopButton.Enabled = false;
                MusicProgressBar.Enabled = false;
                Pause();
                playList.Next();
                autoplay = true;
                LoadHelper(playList.Get(playList.CurrentTrackIndex));
                playButtonIsPlaying = true;
                isDonePlaying = false;
            }
        }

        private void PreviousButton_Click(object sender, EventArgs e)
        {
            if (PreviousButton.Enabled)
            {
                PlayButton.Enabled = false;
                StopButton.Enabled = false;
                MusicProgressBar.Enabled = false;
                Pause();
                playList.Previous();
                autoplay = true;
                LoadHelper(playList.Get(playList.CurrentTrackIndex));
                playButtonIsPlaying = true;
                isDonePlaying = false;
            }
        }

        private void OnlineSyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            onlineSyncForm = new OnlineSyncForm(onlineManager);
            onlineSyncForm.Show();
        }

        private void TrackFilteringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (trackSelectionForm == null || trackSelectionForm.IsDisposed)
            {
                var midiPlayer = player as MidiPlayer;
                trackSelectionForm = new TrackSelectionForm(trackSelectionManager, midiPlayer.mordhauOutDevice);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.TrackSelectionPos));
                trackSelectionForm.Show();
                trackSelectionForm.Top = coords.Y;
                trackSelectionForm.Left = coords.X;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                hotkeyManager.HotkeyPressed(vkCode);
                if (Enum.TryParse(vkCode.ToString(), out Keys tempkey))
                {
                    soundBoardManager.KeyPressed(tempkey);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void liveInputToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (liveInputForm == null || liveInputForm.IsDisposed)
            {
                liveInputForm = new LiveInputForm(liveMidiManager);
                Point coords = WindowPositionUtils.CheckPosition(ConfigManager.GetCoordsProperty(PropertyItem.LiveMidiPos));
                liveInputForm.Show();
                liveInputForm.Top = coords.Y;
                liveInputForm.Left = coords.X;
            }
        }

        private void GuildLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GuildLibraryForm guildLibraryForm = new GuildLibraryForm(this);
            guildLibraryForm.Show();
        }


        private void TimeSyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (timeSyncForm != null)
                timeSyncForm.Dispose();
            timeSyncForm = new TimeSyncForm(this);
            timeSyncForm.Show();

        }

        private void ReloadButton_Click(object sender, EventArgs e)
        {
            // First grab the Track Filtering settings for our current track
            // Re-load the same midi track from file
            // And re-apply those settings

            // I don't think getting the settings is that easy but we'll try
            // Oh hey it can be.
            var data = trackSelectionManager.GetTrackSelectionData();
            player.LoadFile(currentTrackName);
            trackSelectionManager.SetTrackSelectionData(data);
            trackSelectionManager.SaveTrackManager();
            if (trackSelectionForm != null && !trackSelectionForm.IsDisposed && trackSelectionForm.IsHandleCreated) // Everything I can think to check
                trackSelectionForm.Invoke((MethodInvoker)delegate { trackSelectionForm.Refresh(); }); // Invoking just in case this is on a diff thread somehow
            Refresh();
        }

        public enum Totebots
        {
            Default,
            Bass,
            Synth,
            Percussion
        }

        public enum TotebotTypes
        {
            Dance = 0,
            Retro = 1
        }
        // Totebot object IDs: 
        // Default: 1c04327f-1de4-4b06-92a8-2c9b40e491aa
        // Bass: 161786c1-1290-4817-8f8b-7f80de755a06
        // Synth: a052e116-f273-4d73-872c-924a97b86720
        // Perc: 4c6e27a2-4c35-4df3-9794-5e206fef9012
        private Dictionary<Totebots, string> TotebotIds = new Dictionary<Totebots, string>()
        {
            { Totebots.Default,  "1c04327f-1de4-4b06-92a8-2c9b40e491aa"},
            { Totebots.Bass,  "161786c1-1290-4817-8f8b-7f80de755a06" },
            { Totebots.Synth,  "a052e116-f273-4d73-872c-924a97b86720"},
            { Totebots.Percussion, "4c6e27a2-4c35-4df3-9794-5e206fef9012" }
        };


        public class SMNote
        {
            public int startTicks { get; set; }
            public int durationTicks { get; set; }
            public int noteNum { get; set; }
            public int channel { get; set; }
            public int velocity { get; set; } // IDK, not yet implemented but could be
            // Some velocity values seem to not work for some notes, it's weird.  Maybe if they're not a multiple of 5?
            public Totebots instrument { get; set; }
            public TotebotTypes flavor { get; set; }
            public ToteHead totehead { get; set; }
            public MidiEvent midiEvent { get; set; }
            public int internalId { get; set; }
            public ChannelMessage filtered { get; set; }
            public SMTimer startTimer { get; set; }
            public SMTimer durationTimer { get; set; }
        }

        public class SMTimer
        {
            private List<SMNote> attachedNotes = new List<SMNote>();
            public List<SMNote> AttachedNotes { get { return attachedNotes; } }
            private int durationTicks;
            public int DurationTicks
            {
                get { return durationTicks; }
                set {
                    if (value > 40) // 40 game ticks/second
                    {
                        DurationSeconds = value / 40;
                        durationTicks = value % 40;
                    }
                    else
                        durationTicks = value;
                }
            }
            public int DurationSeconds { get; set; } // Auto-sets from ticks
            public ToteHead Totehead { get; set; }
            public int InternalId { get; set; }
            public int Id { get; set; }
            private List<SMTimer> linkedTimers = new List<SMTimer>();
            public List<SMTimer> LinkedTimers { get { return linkedTimers; } }
            public int NorGateId { get; set; }
            public int AndGateId { get; set; }

        }

        public class ToteHead
        {
            public Totebots Instrument { get; set; }
            public TotebotTypes Flavor { get; set; }
            public int Id { get; set; }
            public int Note { get; set; }
            public int InternalId { get; set; }
            private List<SMNote> playingNotes = new List<SMNote>();
            public List<SMNote> PlayingNotes { get { return playingNotes; } }
            public int SetGateId { get; set; }
            public int ResetGateId { get; set; }
            public int OrGateId { get; set; }
        }

        public void SetToteHeadForNote(SMNote note)
        {
            var availableTotes = toteHeads.Where(t => t.Instrument == note.instrument && t.Note == note.noteNum && t.Flavor == note.flavor && (t.PlayingNotes.Count == 0 || t.PlayingNotes.All(pn => pn.startTicks + pn.durationTicks < note.startTicks - 1 || note.startTicks + note.durationTicks < pn.startTicks - 1)));
            //var availableTotes = new List<ToteHead>();
            ToteHead tote;
            if (availableTotes.Count() == 0)
            {
                // Make a new Tote and return it
                tote = new ToteHead() { Flavor = note.flavor, Instrument = note.instrument, Note = note.noteNum, Id = -1, InternalId = toteHeads.Count }; // We mark ID as unset yet
                toteHeads.Add(tote);
            }
            else
                tote = availableTotes.First();
            tote.PlayingNotes.Add(note);
            note.totehead = tote;

        }

        // Meant to be called after a tote is assigned of course
        public void SetDurationTimerForNote(SMNote note)
        {
            //var availableTimers = durationTimers.Where(t => t.Totehead.InternalId == note.totehead.InternalId && t.DurationTicks + t.DurationSeconds*40 == note.durationTicks && t.AttachedNotes.All(an => an.startTicks + an.durationTicks < note.startTicks - 3 || note.startTicks + note.durationTicks < an.startTicks - 3));
            var availableTimers = new List<SMTimer>(); // Always make a new timer, we can't re-use circuits
            SMTimer timer;
            if (availableTimers.Count() == 0)
            {
                timer = new SMTimer() { DurationTicks = note.durationTicks, Totehead = note.totehead, InternalId = durationTimers.Count };
                durationTimers.Add(timer);
            }
            else
                timer = availableTimers.First();

            note.durationTimer = timer;
            timer.AttachedNotes.Add(note);
            SetExtensionTimers(timer);
        }

        // Meant to be called after a tote is assigned of course
        public void SetStartTimerForNote(SMNote note)
        { // These will have to be the same note and everything...which is handled by being the same tote
            // This will basically never have anything in it except in very rare cases
            var availableTimers = startTimers.Where(t => t.Totehead.InternalId == note.totehead.InternalId && t.DurationTicks + t.DurationSeconds * 40 == note.startTicks && t.AttachedNotes.All(an => an.durationTicks == note.durationTicks));
            SMTimer timer;
            if (availableTimers.Count() == 0)
            {
                timer = new SMTimer() { DurationTicks = note.startTicks, Totehead = note.totehead, InternalId = startTimers.Count };
                startTimers.Add(timer);
            }
            else
                timer = availableTimers.First();
            note.startTimer = timer;
            timer.AttachedNotes.Add(note);
            SetExtensionTimers(timer);
        }

        public void SetExtensionTimers(SMTimer timer)
        {
            if (timer.DurationSeconds > 60 || (timer.DurationSeconds == 60 && timer.DurationTicks > 0))
            {
                int extensionNum = (timer.DurationSeconds / 60) - 1;
                SMTimer extension;
                if (extensionNum >= extensionTimers.Count)
                {
                    // Make a new extension timer, linked to by the previous one
                    // We assume we're only 1 above, if not, a lot of things are probably wrong everywhere - these have to go in order
                    extension = new SMTimer() { InternalId = extensionTimers.Count, DurationSeconds = 60 };
                    if (extensionNum > 0)
                        extensionTimers[extensionNum - 1].LinkedTimers.Add(extension);
                    extensionTimers.Add(extension);
                }
                else
                {
                    extension = extensionTimers[extensionNum];
                }
                // Link the existing extension timer to this one
                extension.LinkedTimers.Add(timer);
                timer.DurationSeconds = timer.DurationSeconds % 60;

            }
        }

        private List<ToteHead> toteHeads = new List<ToteHead>();
        private List<SMTimer> durationTimers = new List<SMTimer>();
        private List<SMTimer> startTimers = new List<SMTimer>();
        private List<SMTimer> extensionTimers = new List<SMTimer>();

        // Requires that you pass it a sorted list that happens in order
        private string getSMBlueprint(List<SMNote> notes)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"bodies\":[{\"childs\":[");

            int xAxis = -2;
            int zAxis = -1;
            int posX = 0;
            int posY = 0;
            int posZ = 0;

            List<List<int>> timerComponents = new List<List<int>>(); // has 1 list for each timer, containing all the Ids that it should connect to
            timerComponents.Add(new List<int>()); // List 0 is for our switch

            int id = 0; // We'll use and increment this everytime we place a part, easy enough

            foreach (var note in notes)
            {
                SetToteHeadForNote(note);
                SetStartTimerForNote(note);
                SetDurationTimerForNote(note);
            }

            // Okay... Our steps, probably in this order
            // Update/set all our notes' timers and such
            // Create each totebot-setup with the RS-latches for each totebot in our list
            // Create each timer in our durationTimer list, linking them to the appropriate totebot NOR gate
            // Create each timer in our startTimer list, linking them to the appropriate durationTimer
            // Create each timer in our extensionTimers list, linking them to the appropriate startTimers
            // Add a button cuz it's cooler than the switch and should work the same, we can't turn them off without adding more chips, linking to start timers that have no extension...
            // We'll do this by iterating notes in order until we reach ones that are too far, they're linked to the startTimers

            // In this order, we can get to the Ids after making them through our traversals
            bool first = true;
            foreach (var tote in toteHeads)
            {
                // We need: NOR gate1 linked to OR linked to NOR gate2 linked to OR linked to NOR gate1
                // Gate2 linked to totehead
                tote.Id = id++;
                tote.OrGateId = id++;

                if (first)
                {
                    first = false;
                }
                else
                    sb.Append(","); // So we don't have to worry about things missing
                // Logic OR - Links to totehead, other things link to this
                sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":true,\"controllers\":[{\"id\":" + tote.Id + "}],\"id\":" + tote.OrGateId + ",\"joints\":null,\"mode\":1},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");

                // Totehead, no link
                var r = String.Format("{0:X2}", (int)(255 * (tote.Note / 24f))).ToLower();
                var color = r + r + r; // = ex "a197b9"
                sb.Append(",{\"color\":\"" + color + "\",\"controller\":{\"audioIndex\":" + (int)tote.Flavor + ",\"controllers\":null,\"id\":" + tote.Id + ",\"joints\":null,\"pitch\":" + (tote.Note / 24f) + ",\"volume\":50},\"pos\":{\"x\":" + (posX) + ",\"y\":" + (posY) + ",\"z\":" + (posZ) + "},\"shapeId\":\"" + TotebotIds[tote.Instrument] + "\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }
            // Now create durationTimers, and link them to a Nor gate
            foreach (var timer in durationTimers)
            {
                timer.Id = id++;
                timer.NorGateId = id++;
                timer.AndGateId = id++;
                // Duration Timer, links to Nor gate
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[{\"id\":" + timer.NorGateId + "}],\"id\":" + timer.Id + ",\"joints\":null,\"seconds\":" + timer.DurationSeconds + ",\"ticks\":" + timer.DurationTicks + "},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
                // Logic NOR - Links to and gate
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":true,\"controllers\":[{\"id\":" + timer.AndGateId + "}],\"id\":" + timer.NorGateId + ",\"joints\":null,\"mode\":4},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
                // Logic AND - Links to OR of totebot
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":true,\"controllers\":[{\"id\":" + timer.Totehead.OrGateId + "}],\"id\":" + timer.AndGateId + ",\"joints\":null,\"mode\":0},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }
            // Now create startTimers, and link them to the note's durationTimer.Id and the AND (stored in the note's durationTimer)
            foreach (var timer in startTimers)
            {
                timer.Id = id++;
                // Start Timer, links to durationTimer of each of its notes
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":["); // An empty array here seems fine
                first = true;
                foreach (var note in timer.AttachedNotes)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");
                    sb.Append("{\"id\":" + note.durationTimer.Id + "},{\"id\":" + note.durationTimer.AndGateId + "}");
                }
                // Then finish the timer
                sb.Append("],\"id\":" + timer.Id + ",\"joints\":null,\"seconds\":" + timer.DurationSeconds + ",\"ticks\":" + timer.DurationTicks + "},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }
            // Now create extensionTimers, and link them to their linkedTimers
            foreach (var timer in extensionTimers)
            {
                timer.Id = id++;
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":["); // An empty array here seems fine
                first = true;
                foreach (var linkedTimer in timer.LinkedTimers)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");
                    sb.Append("{\"id\":" + linkedTimer.Id + "}");
                } // Then finish the timer
                sb.Append("],\"id\":" + timer.Id + ",\"joints\":null,\"seconds\":" + timer.DurationSeconds + ",\"ticks\":" + timer.DurationTicks + "},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }
            // And add a switch, linking it to the first extenion and every note's startTimer that has duration 60 seconds or less

            sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":true,\"controllers\":[");
            List<int> addedIds = new List<int>();
            first = true;
            foreach (SMNote note in notes)
            {
                if (!addedIds.Contains(note.startTimer.Id) && (note.startTimer.DurationSeconds < 60 || (note.startTimer.DurationSeconds == 60 && note.startTimer.DurationTicks == 0)))
                {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");
                    addedIds.Add(note.startTimer.Id);
                    sb.Append("{\"id\":" + note.startTimer.Id + "}");
                }
                else // Should be in order, so break once we're past them
                    break;
            }
            // And to the first extensionTimer, if there are any
            if (extensionTimers.Count > 0)
                sb.Append(",{\"id\":" + extensionTimers[0].Id + "}");
            sb.Append("],\"id\":" + (id++) + ",\"joints\":null},\"pos\":{\"x\":0,\"y\":0,\"z\":1},\"shapeId\":\"7cf717d7-d167-4f2d-a6e7-6b2c70aa3986\",\"xaxis\":3,\"zaxis\":-1}");


            // And finish the json
            sb.Append("]}],\"version\":3}");
            return sb.ToString();
            /*







            for (int i = 0; i < notes.Count; i++)
            {
                SMNote note = notes[i];

                int startSeconds = 0;
                if (note.startTicks > 40) // 40 game ticks/second
                {
                    startSeconds = note.startTicks / 40;
                    note.startTicks = note.startTicks % 40;
                }

                startSeconds -= 60 * (timerComponents.Count - 1); // Remove 60 seconds for each timer we've already made

                if (startSeconds >= 60)
                {
                    timerComponents.Add(new List<int>());
                    startSeconds -= 60;
                    // We've hit the limit for how much our timers can delay
                    // Increase to the next timer and decrement a minute
                }

                // timerComponents should never be empty... 
                timerComponents[timerComponents.Count - 1].Add(i * 5);

                double pitch = note.noteNum / 24f; // 25 notes, we have 3 C's oddly enough... 
                                                   // Build our objects... 

                // Here's our base Totebot head, id i*5
                //sb.Append("{\"color\":\"49642d\",\"controller\":{\"audioIndex\":" + (int)note.flavor + ",\"controllers\":null,\"id\":" + (i * 5) + ",\"joints\":null,\"pitch\":" + pitch + ",\"volume\":100},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"" + TotebotIds[note.instrument] + "\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "},");




                





                // Logic (And) - Links to Totebot head, id i*5+1
                sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[{\"id\":" + totehead.Id + "}],\"id\":" + (i * 5 + 1) + ",\"joints\":null,\"mode\":0},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "},");
                // Logic (Nor) - Links to Logic And, id i*5+2
                sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":true,\"controllers\":[{\"id\":" + (i * 5 + 1) + "}],\"id\":" + (i * 5 + 2) + ",\"joints\":null,\"mode\":4},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "},");
                // Timer - How long to play current note - links to our Nor, id i*5+3
                sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[{\"id\":" + (i * 5 + 2) + "}],\"id\":" + (i * 5 + 3) + ",\"joints\":null,\"seconds\":0,\"ticks\":" + note.durationTicks + "},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "},");
                // Timer - How long to delay before playing our note - links to our DurationTimer and our And, id i*5+4
                sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[{\"id\":" + (i * 5 + 1) + "},{\"id\":" + (i * 5 + 3) + "}],\"id\":" + (i * 5 + 4) + ",\"joints\":null,\"seconds\":" + startSeconds + ",\"ticks\":" + note.startTicks + "},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "},");
            }
            // Slap a switch on at the end
            // And all the intermediate timers we need

            // Switch - links to every start Timer at i*5+4
            sb.Append("{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[");
            for (int i = 0; i < timerComponents[0].Count; i++) // Guaranteed to run at least once
            {
                if (i != 0)
                    sb.Append(",");
                sb.Append("{\"id\":" + (timerComponents[0][i] + 4) + "}"); // timerComponents is our base, +4 is our start Timer that it links to
            }
            // TODO: Then also links to every And no matter what so it actually works
            for(int i = 0; i < notes.Count; i++)
            {
                // These are id i*5+1
                sb.Append(",{\"id\":" + (i * 5 + 1) + "}");
            }

            // Link it to only the first timer we're about to make, if any, id notes.Count*5+1
            if (timerComponents.Count > 1)
                sb.Append(",{\"id\":" + (notes.Count * 5 + 1) + "}");
            // Finish the switch
            sb.Append("],\"id\":" + (notes.Count * 5) + ",\"joints\":null},\"pos\":{\"x\":0,\"y\":0,\"z\":1},\"shapeId\":\"7cf717d7-d167-4f2d-a6e7-6b2c70aa3986\",\"xaxis\":3,\"zaxis\":-1}");

            // Make any timers
            for (int i = 1; i < timerComponents.Count; i++)
            {
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[");
                // Iterate the inner list
                bool first = true;
                foreach (int j in timerComponents[i])
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                        sb.Append(",");
                    sb.Append("{\"id\":" + (j + 4) + "}"); // Attach to the timer of the target
                }
                // If there's another timer, link to it
                if (i < timerComponents.Count - 1)
                    sb.Append(",{\"id\":" + (notes.Count * 5 + i + 1) + "}");
                // Finish the timer for 60 seconds
                sb.Append("],\"id\":" + (notes.Count * 5 + i) + ",\"joints\":null,\"seconds\":60,\"ticks\":0},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"8f7fd0e7-c46e-4944-a414-7ce2437bb30f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }

            // Make the totebot heads and OR gates
            // Currently everything points to the totebot.Id, which we'll actually make the OR gates
            // And then heads themselves get Ids starting at notes.Count*5 + timerComponents.Count

            var random = new Random();
            for (int i = 0; i < toteHeads.Count; i++)
            {
                int headId = notes.Count * 5 + timerComponents.Count + i;
                var totehead = toteHeads[i];
                // Make an OR gate
                // Logic (Or) - Links to Totebot head
                sb.Append(",{\"color\":\"df7f01\",\"controller\":{\"active\":false,\"controllers\":[{\"id\":" + headId + "}],\"id\":" + totehead.Id + ",\"joints\":null,\"mode\":1},\"pos\":{\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "},\"shapeId\":\"9f0f56e8-2c31-4d83-996c-d00a9b296c3f\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");

                // Make the head.  You can actually see the color poke through when it plays
                // So let's make the color black to white dependent on the pitch
                
                var color = String.Format("{0:X6}", 0x1000000 * totehead.Note / 24f).ToLower(); // = ex "a197b9"
                sb.Append(",{\"color\":\"" + color + "\",\"controller\":{\"audioIndex\":" + (int)totehead.Flavor + ",\"controllers\":null,\"id\":" + headId + ",\"joints\":null,\"pitch\":" + (totehead.Note / 24f) + ",\"volume\":50},\"pos\":{\"x\":" + posX + ",\"y\":" + (posY) + ",\"z\":" + (posZ) + "},\"shapeId\":\"" + TotebotIds[totehead.Instrument] + "\",\"xaxis\":" + xAxis + ",\"zaxis\":" + zAxis + "}");
            }


            sb.Append("]}],\"version\":3}");
            return sb.ToString();
            */
        }

        private void authToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var authForm = new DiscordAuthForm();
            authForm.Show(this);
        }

        private void lutemodPartitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (partitionsForm == null || partitionsForm.IsDisposed)
                partitionsForm = new PartitionsForm(trackSelectionManager, player);
            partitionsForm.Show();
        }
    }
}
