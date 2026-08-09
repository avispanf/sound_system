namespace AudioMW
{
    public struct SoundHandle
    {
        public static readonly SoundHandle None = new SoundHandle(0);

        private readonly int id;

        public SoundHandle(int id)
        {
            this.id = id;
        }

        public int Id
        {
            get { return id; }
        }

        public bool IsAssigned
        {
            get { return id != 0; }
        }

        public bool IsPlaying
        {
            get { return IsAssigned && AudioRuntime.Exists && AudioRuntime.Instance.IsHandlePlaying(id); }
        }

        public bool IsVirtual
        {
            get { return IsAssigned && AudioRuntime.Exists && AudioRuntime.Instance.IsHandleVirtual(id); }
        }

        public Voice Voice
        {
            get { return IsAssigned && AudioRuntime.Exists ? AudioRuntime.Instance.GetHandleVoice(id) : null; }
        }

        public void Stop()
        {
            if (IsAssigned && AudioRuntime.Exists)
            {
                AudioRuntime.Instance.StopHandle(id);
            }
        }

        public void SetParameter(SoundParameter parameter, float value)
        {
            Voice voice = Voice;

            if (voice != null)
            {
                voice.SetLocalParameter(parameter, value);
            }
        }

        public override string ToString()
        {
            return IsAssigned ? "SoundHandle(" + id + ")" : "SoundHandle(none)";
        }
    }
}
