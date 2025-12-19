using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonoMod.RuntimeDetour;

namespace GodhomeQoL.Modules.QoL;

public sealed class CollectorRoarMute : Module
{
    private static AudioClip? customClip;

    public override bool DefaultEnabled => true;

    private Hook? hookPlay;
    private Hook? hookPlayDelayed;
    private Hook? hookPlayScheduled;
    private Hook? hookPlayOneShot;
    private Hook? hookPlayClipAtPoint;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

    private protected override void Load()
    {
        LoadCustomClip();

        hookPlay ??= new Hook(
            typeof(AudioSource).GetMethod(nameof(AudioSource.Play), Array.Empty<Type>())!,
            OnPlay
        );
        hookPlayDelayed ??= new Hook(
            typeof(AudioSource).GetMethod(nameof(AudioSource.PlayDelayed))!,
            OnPlayDelayed
        );
        hookPlayScheduled ??= new Hook(
            typeof(AudioSource).GetMethod(nameof(AudioSource.PlayScheduled))!,
            OnPlayScheduled
        );
        hookPlayOneShot ??= new Hook(
            typeof(AudioSource).GetMethod(nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip), typeof(float) })!,
            OnPlayOneShot
        );
        hookPlayClipAtPoint ??= new Hook(
            typeof(AudioSource).GetMethod(nameof(AudioSource.PlayClipAtPoint), new[] { typeof(AudioClip), typeof(Vector3), typeof(float) })!,
            OnPlayClipAtPoint
        );
    }

    private protected override void Unload()
    {
        hookPlay?.Dispose();
        hookPlay = null;
        hookPlayDelayed?.Dispose();
        hookPlayDelayed = null;
        hookPlayScheduled?.Dispose();
        hookPlayScheduled = null;
        hookPlayOneShot?.Dispose();
        hookPlayOneShot = null;
        hookPlayClipAtPoint?.Dispose();
        hookPlayClipAtPoint = null;

        customClip = null;
    }

    private static bool ShouldReplace(AudioClip? clip)
    {
        if (clip == null || Ref.GM == null || customClip == null)
        {
            return false;
        }

        string scene = Ref.GM.sceneName;
        if (scene != "GG_Collector" && scene != "GG_Collector_V")
        {
            return false;
        }

        return string.Equals(clip.name, "Collector_Roar", StringComparison.Ordinal);
    }

    private static void OnPlay(Action<AudioSource> orig, AudioSource self)
    {
        if (ShouldReplace(self.clip))
        {
            self.clip = customClip;
            LogDebug("CollectorRoarMute: replaced in Play");
        }
        orig(self);
    }

    private static void OnPlayDelayed(Action<AudioSource, float> orig, AudioSource self, float delay)
    {
        if (ShouldReplace(self.clip))
        {
            self.clip = customClip;
            LogDebug("CollectorRoarMute: replaced in PlayDelayed");
        }
        orig(self, delay);
    }

    private static void OnPlayScheduled(Action<AudioSource, double> orig, AudioSource self, double time)
    {
        if (ShouldReplace(self.clip))
        {
            self.clip = customClip;
            LogDebug("CollectorRoarMute: replaced in PlayScheduled");
        }
        orig(self, time);
    }

    private static void OnPlayOneShot(Action<AudioSource, AudioClip, float> orig, AudioSource self, AudioClip clip, float volumeScale)
    {
        if (ShouldReplace(clip))
        {
            clip = customClip!;
            LogDebug("CollectorRoarMute: replaced in PlayOneShot");
        }
        orig(self, clip, volumeScale);
    }

    private static void OnPlayClipAtPoint(Action<AudioClip, Vector3, float> orig, AudioClip clip, Vector3 position, float volume)
    {
        if (ShouldReplace(clip))
        {
            clip = customClip!;
            LogDebug("CollectorRoarMute: replaced in PlayClipAtPoint");
        }
        orig(clip, position, volume);
    }

    private static void LoadCustomClip()
    {
        try
        {
            Assembly asm = typeof(CollectorRoarMute).Assembly;
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Collector_Roar.wav", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using Stream? stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    customClip = LoadWav(stream);
                    if (customClip != null)
                    {
                        LogDebug($"CollectorRoarMute: loaded custom clip '{customClip.name}' from embedded resource '{resourceName}'");
                        return;
                    }
                }
                LogDebug($"CollectorRoarMute: failed to load custom clip from embedded resource '{resourceName}'");
            }
            else
            {
                LogDebug("CollectorRoarMute: embedded resource Collector_Roar.wav not found, falling back to external file");
            }

            string dir = Path.GetDirectoryName(asm.Location)
                ?? AppDomain.CurrentDomain.BaseDirectory
                ?? Environment.CurrentDirectory;
            string path = Path.Combine(dir, "Collector_Roar.wav");

            if (!File.Exists(path))
            {
                LogDebug($"CollectorRoarMute: custom clip not found at {path}");
                return;
            }

            customClip = LoadWav(File.OpenRead(path));
            if (customClip != null)
            {
                LogDebug($"CollectorRoarMute: loaded custom clip '{customClip.name}' from {path}");
            }
            else
            {
                LogDebug($"CollectorRoarMute: failed to load custom clip from {path}");
            }
        }
        catch (Exception e)
        {
            LogDebug($"CollectorRoarMute: exception loading custom clip - {e.Message}");
            customClip = null;
        }
    }

    private static AudioClip? LoadWav(Stream stream)
    {
        using BinaryReader br = new(stream, Encoding.UTF8, leaveOpen: true);

        if (new string(br.ReadChars(4)) != "RIFF")
            return null;
        br.ReadInt32(); // file size
        if (new string(br.ReadChars(4)) != "WAVE")
            return null;

        // fmt chunk
        string fmt = new string(br.ReadChars(4));
        if (fmt != "fmt ")
            return null;
        int fmtSize = br.ReadInt32();
        short audioFormat = br.ReadInt16();
        short channels = br.ReadInt16();
        int sampleRate = br.ReadInt32();
        br.ReadInt32(); // byteRate
        br.ReadInt16(); // blockAlign
        short bitsPerSample = br.ReadInt16();
        // skip any extra fmt bytes
        if (fmtSize > 16)
        {
            br.ReadBytes(fmtSize - 16);
        }

        // find data chunk
        string dataHeader = new string(br.ReadChars(4));
        while (dataHeader != "data")
        {
            int chunkSize = br.ReadInt32();
            br.ReadBytes(chunkSize);
            dataHeader = new string(br.ReadChars(4));
        }
        int dataSize = br.ReadInt32();

        if (audioFormat != 1)
        {
            return null; // only PCM
        }

        int sampleCount = dataSize / (bitsPerSample / 8);
        float[] data = new float[sampleCount];

        if (bitsPerSample == 16)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = br.ReadInt16();
                data[i] = sample / 32768f;
            }
        }
        else if (bitsPerSample == 8)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                byte sample = br.ReadByte();
                data[i] = (sample - 128) / 128f;
            }
        }
        else
        {
            return null;
        }

        AudioClip clip = AudioClip.Create("Collector_Roar", sampleCount / channels, channels, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
