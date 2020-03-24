﻿using LuteBot.Config;
using LuteBot.TrackSelection;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LuteBot.Core.Midi
{
    public class MidiPlayer : Player
    {
        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;
        public MordhauOutDevice mordhauOutDevice;
        public RustOutDevice rustOutDevice;
        private TrackSelectionManager trackSelectionManager;

        private bool isPlaying;

        public MidiPlayer(TrackSelectionManager trackSelectionManager)
        {
            isPlaying = false;
            mordhauOutDevice = new MordhauOutDevice();
            rustOutDevice = new RustOutDevice();
            this.trackSelectionManager = trackSelectionManager;
            sequence = new Sequence
            {
                Format = 1
            };

            sequencer = new Sequencer
            {
                Position = 0,
                Sequence = this.sequence
            };
            sequencer.PlayingCompleted += new System.EventHandler(this.HandlePlayingCompleted);
            sequencer.ChannelMessagePlayed += new System.EventHandler<Sanford.Multimedia.Midi.ChannelMessageEventArgs>(this.HandleChannelMessagePlayed);
            sequencer.SysExMessagePlayed += new System.EventHandler<Sanford.Multimedia.Midi.SysExMessageEventArgs>(this.HandleSysExMessagePlayed);
            sequencer.Chased += new System.EventHandler<Sanford.Multimedia.Midi.ChasedEventArgs>(this.HandleChased);
            sequencer.Stopped += new System.EventHandler<Sanford.Multimedia.Midi.StoppedEventArgs>(this.HandleStopped);

            if (!(OutputDevice.DeviceCount == 0))
            {
                outDevice = new OutputDevice(ConfigManager.GetIntegerProperty(PropertyItem.OutputDevice));
                sequence.LoadCompleted += HandleLoadCompleted;
            }
        }

        public void ResetDevice()
        {
            outDevice.Reset();
            outDevice.Dispose();
            outDevice = new OutputDevice(ConfigManager.GetIntegerProperty(PropertyItem.OutputDevice));
        }

        public void UpdateMutedTracks(TrackItem item)
        {
            if (isPlaying)
            {
                sequencer.Stop();
            }
            sequence.UpdateMutedTracks(item.Id, item.Active);
            if (isPlaying)
            {
                sequencer.Continue();
            }
            outDevice.Reset();
        }

        public List<int> GetMidiChannels()
        {
            return sequence.Channels;
        }

        public List<string> GetMidiTracks()
        {
            return sequence.TrackNames();
        }

        public override int GetLenght()
        {
            return sequence.GetLength();
        }

        public override string GetFormattedPosition()
        {
            TimeSpan time = TimeSpan.FromSeconds(0);
            time = TimeSpan.FromSeconds((int)((sequencer.Position / sequence.Division) * this.sequence.FirstTempo / 1000000f));
            return time.ToString(@"mm\:ss");
        }

        public override string GetFormattedLength()
        {
            TimeSpan time = TimeSpan.FromSeconds(0);
            time = TimeSpan.FromSeconds((int)((sequence.GetLength() / sequence.Division) * this.sequence.FirstTempo / 1000000f));
            return time.ToString(@"mm\:ss");
        }

        public override void SetPosition(int position)
        {
            sequencer.Position = position;
        }

        public override int GetPosition()
        {
            return sequencer.Position;
        }

        public override void ResetSoundEffects()
        {
            outDevice.Reset();
        }

        public override void LoadFile(string filename)
        {
            sequence.LoadAsync(filename);
            /*
            if (filename.Contains(@"\"))
            {
                // Trim it to just the midi name
                filename = filename.Substring(filename.LastIndexOf(@"\"));
                filename = filename.Replace(".mid", ".lua");
                mcPath = $@"C:\Users\DMentia\AppData\Roaming\LuteBot\{filename}";
            }
            */
        }
        //private string mcPath = @"C:\Users\DMentia\AppData\Roaming\LuteBot\minecraftSong.txt";

        /*
        private void FinalizeMC()
        {
            if (mordhauOutDevice != null && mordhauOutDevice.McFile != null)
            {
                string entireFile = mordhauOutDevice.McFile.ToString();
                entireFile = entireFile.Insert(0,
                    "local speed = 1\r\n\r\n\r\nlocal speaker = peripheral.find(\"speaker\")\r\n");
                // Now check through each MidiChannel type, and if it's been used we instantiate it up top
                for (int i = 0; i < MidiChannelTypes.Names.Length; i++)
                {
                    string realName = MidiChannelTypes.Names[i].Replace(" ", "").Replace("-", "");
                    if (entireFile.Contains(realName))
                    {
                        entireFile = entireFile.Insert(0, $"_G.{realName}{i} = \"Guitar\"\r\n");
                    }
                }

                using (StreamWriter writer = new StreamWriter(mcPath, false))
                    writer.Write(entireFile);

                entireFile = null;
                mordhauOutDevice.McFile = null;
            }
        }
        */

        public override void Stop()
        {
            //FinalizeMC();
            isPlaying = false;
            sequencer.Stop();
            sequencer.Position = 0;
            outDevice.Reset();
        }

        public override void Play()
        {
            //if (File.Exists(mcPath))
            //    File.Delete(mcPath);
            //mordhauOutDevice.McFile = new StringBuilder();
            //mordhauOutDevice.stopWatch.Restart();

            //midiOut = Melanchall.DryWetMidi.Devices.OutputDevice.GetByName("Rust");

            isPlaying = true;
            if (sequencer.Position > 0)
            {
                sequencer.Continue();
            }
            else
            {
                sequencer.Start();
            }
        }

        public override void Pause()
        {
            //midiOut.Dispose();
            isPlaying = false;
            sequencer.Stop();
            outDevice.Reset();
        }

        public override void Dispose()
        {
            this.disposed = true;
            sequence.Dispose();

            if (outDevice != null)
            {
                outDevice.Dispose();
            }
        }

        //------------- Handlers -------------

        private void HandleLoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                // We need to manually parse out data for Max and Min note for each Channel, in trackSelectionManager
                trackSelectionManager.MaxNoteByChannel = new Dictionary<int, int>();
                trackSelectionManager.MinNoteByChannel = new Dictionary<int, int>();
                foreach(var track in sequence)
                {
                    foreach(var midiEvent in track.Iterator())
                    {

                        if (midiEvent.MidiMessage is ChannelMessage c && c.Data2 > 0 && c.Command == ChannelCommand.NoteOn)
                        {
                            if (!trackSelectionManager.MaxNoteByChannel.ContainsKey(c.MidiChannel))
                                trackSelectionManager.MaxNoteByChannel.Add(c.MidiChannel, c.Data1);
                            else if (trackSelectionManager.MaxNoteByChannel[c.MidiChannel] < c.Data1)
                                trackSelectionManager.MaxNoteByChannel[c.MidiChannel] = c.Data1;

                            if (!trackSelectionManager.MinNoteByChannel.ContainsKey(c.MidiChannel))
                                trackSelectionManager.MinNoteByChannel.Add(c.MidiChannel, c.Data1);
                            else if (trackSelectionManager.MinNoteByChannel[c.MidiChannel] > c.Data1)
                                trackSelectionManager.MinNoteByChannel[c.MidiChannel] = c.Data1;
                        }
                    }
                }

                base.LoadCompleted(this, e);
                mordhauOutDevice.HighMidiNoteId = sequence.MaxNoteId;
                mordhauOutDevice.LowMidiNoteId = sequence.MinNoteId;
                rustOutDevice.HighMidiNoteId = sequence.MaxNoteId;
                rustOutDevice.LowMidiNoteId = sequence.MinNoteId;
            }
        }

        private void HandleStopped(object sender, StoppedEventArgs e)
        {
            foreach (ChannelMessage message in e.Messages)
            {
                if (ConfigManager.GetBooleanProperty(PropertyItem.SoundEffects) && !disposed)
                {
                    outDevice.Send(message);
                }
            }
        }

        private void HandleChased(object sender, ChasedEventArgs e)
        {
            if (ConfigManager.GetBooleanProperty(PropertyItem.SoundEffects) && !disposed)
            {
                foreach (ChannelMessage message in e.Messages)
                {
                    outDevice.Send(message);
                }
            }
        }

        private void HandleSysExMessagePlayed(object sender, SysExMessageEventArgs e)
        {
        }

        private void HandleChannelMessagePlayed(object sender, ChannelMessageEventArgs e)
        {
            if (ConfigManager.GetProperty(PropertyItem.NoteConversionMode) != "1")
            {
                if (ConfigManager.GetBooleanProperty(PropertyItem.SoundEffects) && !disposed)
                {
                    var filtered = trackSelectionManager.FilterMidiEvent(e.Message, e.TrackId);
                    //if (e.Message.Data2 > 0) // Avoid spamming server with 0 velocity notes
                    //{

                    //Melanchall.DryWetMidi.Core.NoteEvent nEvent;
                    // I don't know how to do this without Melanchall package but it's gonna be a bitch to convert the event types here...


                    //midiOut.SendEvent(new Melanchall.DryWetMidi.Core.NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)filtered.Data1, (Melanchall.DryWetMidi.Common.SevenBitNumber)filtered.Data2));
                    // Below: Sound to match, then unfiltered sound.  Or leave commented for no sound.
                    if (!outDevice.IsDisposed) // If they change song prefs while playing, this can fail, so just skip then
                        try
                        {
                            var note = rustOutDevice.FilterNote(filtered, trackSelectionManager.NoteOffset +
                                (trackSelectionManager.MidiChannelOffsets.ContainsKey(e.Message.MidiChannel) ? trackSelectionManager.MidiChannelOffsets[e.Message.MidiChannel] : 0));
                            if(note != null)
                                outDevice.Send(note);
                        }
                        catch (Exception) { } // Ignore exceptions, again, they might edit things while it's trying to play
                    //}
                    //outDevice.Send(filtered);
                }
                else
                {
                    mordhauOutDevice.SendNote(trackSelectionManager.FilterMidiEvent(e.Message, e.TrackId), trackSelectionManager.NoteOffset + 
                       (trackSelectionManager.MidiChannelOffsets.ContainsKey(e.Message.MidiChannel) ? trackSelectionManager.MidiChannelOffsets[e.Message.MidiChannel] : 0));
                }
            }
            else
            {
                outDevice.Send(trackSelectionManager.FilterMidiEvent(e.Message, e.TrackId));
            }
        }

        private void HandlePlayingCompleted(object sender, EventArgs e)
        {
            //FinalizeMC();
            //midiOut.Dispose();
            isPlaying = false;
            outDevice.Reset();            
        }
    }
}

