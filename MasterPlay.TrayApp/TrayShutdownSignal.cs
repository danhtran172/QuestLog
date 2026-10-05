namespace MasterPlay.TrayApp
{
    /// <summary>
    /// Shell (QuestLog.exe) bat su kien nay truoc khi cap nhat/go cai dat de tray
    /// thoat em — ghi not giay choi dang dem — thay vi bi trinh cap nhat giet cung.
    /// Ten phai khop voi MasterPlay.Shell/TrayProcess.cs.
    /// </summary>
    internal sealed class TrayShutdownSignal : IDisposable
    {
        private const string EventName = "QuestLogTrayShutdownRequest";

        private readonly EventWaitHandle _event;
        private readonly RegisteredWaitHandle _registration;

        public TrayShutdownSignal(Action onShutdownRequested)
        {
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _event,
                (_, _) => onShutdownRequested(),
                state: null,
                Timeout.Infinite,
                executeOnlyOnce: true);
        }

        public void Dispose()
        {
            _registration.Unregister(null);
            _event.Dispose();
        }
    }
}
