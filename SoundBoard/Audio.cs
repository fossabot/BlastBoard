﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BlastBoard
{
    //Code form the creator of the library.
    //I did changes to AudioPlayBackEngine() and Dispose().
    //Also added ResampleTo44100().
    class AudioPlaybackEngine : IDisposable
    {
        private readonly WaveOut SpeakersOUT;
        private readonly WaveOut VACinputOUT;
        private readonly MixingSampleProvider mixer1;
        private readonly MixingSampleProvider mixer2;

        public float LocalVolume
        {
            get { return SpeakersOUT.Volume * 100.0f; }
            set { SpeakersOUT.Volume = value / 100.0f; }
        }

        public float OutputVolume
        {
            get { return VACinputOUT.Volume * 100.0f; }
            set { VACinputOUT.Volume = value / 100.0f; }
        }

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
            SpeakersOUT = new WaveOut();
            SpeakersOUT.DeviceNumber = Properties.Settings.Default.Speakers;
            VACinputOUT = new WaveOut();
            VACinputOUT.DeviceNumber = Properties.Settings.Default.VACinput;

            //We need 2 mixers because we need 2 readers, one for wich output.
            //If we share a reader it will divide "sound frames" across the 2 outputs
            //this would result in skipping "sound frames".
            mixer1 = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
            mixer1.ReadFully = true;
            mixer2 = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
            mixer2.ReadFully = true;

            SpeakersOUT.Init(mixer1);
            VACinputOUT.Init(mixer2);
            SpeakersOUT.Play();
            VACinputOUT.Play();
        }

        public void StopSound()
        {
            mixer1.RemoveAllMixerInputs();
            mixer2.RemoveAllMixerInputs();
        }

        //Receives the file and sends to set up auto disposal.
        public void PlaySound(string fileName)
        {
            var input1 = new AutoDisposeFileReader(new AudioFileReader(fileName));
            var input2 = new AutoDisposeFileReader(new AudioFileReader(fileName));
            AddMixerInput(input1, input2);
        }

        //Converts to mono to stereo.
        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer1.WaveFormat.Channels && input.WaveFormat.Channels == mixer2.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && mixer1.WaveFormat.Channels == 2 && mixer2.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }
            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        //Resamples into the right sample rate.
        private ISampleProvider ResampleTo44100(ISampleProvider input)
        {
            if (input.WaveFormat.SampleRate != mixer1.WaveFormat.SampleRate && input.WaveFormat.SampleRate != mixer2.WaveFormat.SampleRate)
            {
                // Converts the ISampleProvider to IWaveProvider,
                // resamples the created IWaveProvider to 44100,
                // converts the IWaveProvider back to ISampleProvider.
                return new WaveToSampleProvider(new MediaFoundationResampler(new SampleToWaveProvider(input), 44100));
            }
            if (input.WaveFormat.SampleRate == mixer1.WaveFormat.SampleRate && input.WaveFormat.SampleRate == mixer2.WaveFormat.SampleRate)
            {
                return input;
            }
            throw new Exception("WTF! How the fuck you made the mixers have diferent sample rates?");
        }

        //Add disposable reader to the mixer input after convertion to stereo.
        private void AddMixerInput(ISampleProvider input1, ISampleProvider input2)
        {
            mixer1.AddMixerInput(ResampleTo44100(ConvertToRightChannelCount(input1)));
            mixer2.AddMixerInput(ResampleTo44100(ConvertToRightChannelCount(input2)));
        }

        public void Dispose()
        {
            if (SpeakersOUT != null)
            {
                SpeakersOUT.Stop();
                SpeakersOUT.Dispose();
            }
            if (VACinputOUT != null)
            {
                VACinputOUT.Stop();
                VACinputOUT.Dispose();
            }
        }
    }


    //Automaticly disposes readers.
    class AutoDisposeFileReader : ISampleProvider
    {
        private readonly AudioFileReader reader;
        private bool isDisposed;
        public AutoDisposeFileReader(AudioFileReader reader)
        {
            this.reader = reader;
            this.WaveFormat = reader.WaveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (isDisposed)
                return 0;
            int read = reader.Read(buffer, offset, count);
            if (read == 0)
            {
                reader.Dispose();
                isDisposed = true;
            }
            return read;
        }

        public WaveFormat WaveFormat { get; private set; }
    }
}   
