namespace TDious.Services
{
    public class LifecycleService
    {
        public event Func<Task>? OnResume;

        public async Task RaiseOnResumeAsync()
        {
            if (OnResume is not null)
            {
                await OnResume.Invoke();
            }
        }
    }
}
