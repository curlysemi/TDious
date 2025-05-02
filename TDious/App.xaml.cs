using TDious.Services;

namespace TDious
{
    public partial class App : Application
    {
        private readonly LifecycleService _lifecycleService;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _lifecycleService = serviceProvider.GetRequiredService<LifecycleService>();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "TDious" };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await _lifecycleService.RaiseOnResumeAsync();
        }
    }
}
