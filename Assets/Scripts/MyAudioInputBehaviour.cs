// Template code to adapt Unity microphone input to Wwise AkAudioInput

// Basic strategy is to call UnityEngine.Microphone.Start(), which begins periodic calls to OnAudioFilterRead in the Unity DSP audio processing thread (“producer thread“)
// and then to call AkAudioInputManager.PostAudioInputEvent(), which begins periodic calls to AudioSamplesDelegate in an AkAudioInputManager background thread (“consumer thread“)

// Microphone input samples are written to a buffer in OnAudioFilterRead (“producer function“)
// and read from the buffer in AudioSamplesDelegate (“consumer function“)

// IMPORTANT:  ensure that under Edit –> Project Settings –> Audio, “Disable Unity Audio“ is NOT checked

// This code was created for the MSCA VRAASP Project at the University of Huddersfield by Kristina Wolfe and Doug Swanson.  NOt for commercial use.

using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class MyAudioInputBehaviour : MonoBehaviour
{
    // 
    public AK.Wwise.Event MicEvent;

    public AudioMixerGroup mixer;

    // number of audio input channels (must be 1, since Audiokinetic only supports mono inputs)
    public uint NumberOfChannels;

    // sample rate of input signal in Hz (should be either 44100 or 48000)
    public uint SampleRate;

    // set to a reasonable value like 10 seconds
    public uint BufferLengthInSeconds;

    // used for recording microphone input
    private AudioSource src;

    // internal buffer of samples produced by microphone in OnAudioFilterRead and consumed by Wwise in AudioSamplesDelegate
    private List<float> buffer = new List<float>();

    // synchronizes access to buffer since OnAudioFilterRead and AudioSamplesDelegate execute in different threads
    private Mutex mutex = new Mutex();

    // can be used to stop recording at runtime
    private bool IsPlaying = true;

    // Wwise callback that sends buffered samples to Wwise (“consumer thread“)
    bool AudioSamplesDelegate(uint playingID, uint channelIndex, float[] samples)
    {
        // acquire ownership of mutex and buffer
        mutex.WaitOne();

        // copy samples from buffer to temporary block
        int blockSize = Math.Min(buffer.Count, samples.Length);
        List<float> block = buffer.GetRange(0, blockSize);
        buffer.RemoveRange(0, blockSize);

        // release ownership of mutex and buffer (release mutex as quickly as possible)
        mutex.ReleaseMutex();

        // copy samples from temporary block to output array
        block.CopyTo(samples);

        // Return false to indicate that there is no more data to provide. This will also stop the associated event.
        return IsPlaying;
    }

    // Wwise callback that specifies format of samples
    void AudioFormatDelegate(uint playingID, AkAudioFormat audioFormat)
    {
        audioFormat.channelConfig.uNumChannels = NumberOfChannels;
        audioFormat.uSampleRate = SampleRate;
    }

    private void Start()
    {
        // start Unity microphone recording (following http://www.kaappine.fi/tutorials/using–microphone–input–in–unity3d)
        src = GetComponent<AudioSource>();
        src.clip = Microphone.Start(null, true, (int)BufferLengthInSeconds, (int)SampleRate);
        src.loop = true;
        //   src.mute = true;
        src.outputAudioMixerGroup = mixer;
        while (!(Microphone.GetPosition(null) > 0)) { }
    
        src.Play();
        src.ignoreListenerVolume = true;
        // start Wwise “consumer thread“
        AkAudioInputManager.PostAudioInputEvent("mic", gameObject, AudioSamplesDelegate, AudioFormatDelegate);
        

    }

    // Unity callback on microphone input (“producer thread“)
    void OnAudioFilterRead(float[] data, int channels)
    {
        // acquire ownership of mutex and buffer
        mutex.WaitOne();

        // copy samples to buffer (de–interleave channels)
        for (int i = 0; i < data.Length / channels; i++)
            buffer.Add(data[i * channels]);

        // release ownership of mutex and buffer
        mutex.ReleaseMutex();
    }

    // This method can be called by other scripts to stop the callback
    public void StopSound()
    {
        IsPlaying = false;
        src.Stop();
        Microphone.End(null);
     //   MicEvent.Stop(gameObject);

    }

    private void OnDestroy()
    {
        MicEvent.Stop(gameObject);
    }
}