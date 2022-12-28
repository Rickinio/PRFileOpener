using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PRFileOpener.Options
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.OptionsPageDialogOptions), "PRFileOpener.Options", "OptionsPageDialog", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class OptionsPageDialogOptions : BaseOptionPage<OptionsPageDialog> { }
    }

    public class OptionsPageDialog : BaseOptionModel<OptionsPageDialog>
    {
        [Category("PR File Opener")]
        [DisplayName("Target branch name")]
        [Description("Target branch name")]
        public string TargetBranchName { get; set; } = "main";
    }
}
