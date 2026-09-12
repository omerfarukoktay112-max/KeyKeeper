using KeyKeeper.Views;

namespace KeyKeeper
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("login-form", typeof(LoginFormPage));
            Routing.RegisterRoute("card-form", typeof(CardFormPage));
            Routing.RegisterRoute("wifi-form", typeof(WifiFormPage));
            Routing.RegisterRoute("note-form", typeof(NoteFormPage));
            Routing.RegisterRoute("license-form", typeof(LicenseFormPage));
            Routing.RegisterRoute("folder-management", typeof(FolderManagementPage));
            Routing.RegisterRoute("password-generator", typeof(PasswordGeneratorPage));
            Routing.RegisterRoute("backup", typeof(BackupPage));
        }
    }
}
