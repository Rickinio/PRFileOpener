using Microsoft.VisualStudio.Shell.Interop;
using PRFileOpener.Options;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PRFileOpener.Commands
{
    [Command(PackageIds.OpenFilesCommand)]
    internal sealed class OpenFilesCommand : BaseCommand<OpenFilesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var currentSolution = await VS.Solutions.GetCurrentSolutionAsync();
            var currentSolutionPath = Directory.GetParent(currentSolution.FullPath)?.FullName;

            var targetBranchName = OptionsPageDialog.Instance.TargetBranchName;
            if (targetBranchName is null) return;

            var branchGetter = new BranchGetter();
            var currentBranchName = branchGetter.GetCurrentBranchName(currentSolutionPath);

            var filesChanged = new List<string>();
            string stdError = null;
            var nextIsFiles = false;

            using (var cmdProcess = new Process())
            {
                cmdProcess.StartInfo = new ProcessStartInfo()
                {
                    WorkingDirectory = currentSolutionPath,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                cmdProcess.Start();
                cmdProcess.OutputDataReceived += (s, ev) =>
                {
                    if (nextIsFiles)
                    {
                        if (!string.IsNullOrEmpty(ev.Data))
                        {
                            filesChanged.Add(ev.Data);
                        }
                        else
                        {
                            nextIsFiles = false;
                        }

                    }
                    if (ev.Data != null && ev.Data.Contains("git diff --name-only"))
                    {
                        nextIsFiles = true;
                    }
                };
                cmdProcess.ErrorDataReceived += (s, ev) => { stdError += ev.Data + Environment.NewLine; };
                cmdProcess.BeginOutputReadLine();
                cmdProcess.BeginErrorReadLine();

                using (var sw = cmdProcess.StandardInput)
                {
                    await sw.WriteLineAsync("git pull -q");
                    await sw.WriteLineAsync($"git fetch origin {targetBranchName}:{targetBranchName}");
                    await sw.WriteLineAsync($"git diff --name-only {currentBranchName} {targetBranchName}");
                }
                cmdProcess.WaitForExit();
            };

            if (!string.IsNullOrWhiteSpace(stdError))
            {
                await VS.MessageBox.ShowErrorAsync("Error", stdError);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var factory = (IVsThreadedWaitDialogFactory)await VS.Services.GetThreadedWaitDialogAsync();
            ThreadedWaitDialogProgressData progress = new(
                "Opening documents",
                "",
                "",
                isCancelable: true,
                currentStep: 0,
                totalSteps: filesChanged.Count
            );

            using (var session = factory.StartWaitDialog(Vsix.Name, initialProgress: progress))
            {
                for (var i = 0; i < filesChanged.Count; i++)
                {
                    if (session.UserCancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    progress = new ThreadedWaitDialogProgressData(
                        progress.WaitMessage,
                        filesChanged[i],
                        progress.StatusBarText,
                        progress.IsCancelable,
                        i,
                        progress.TotalSteps
                    );

                    session.Progress.Report(progress);

                    await VS.Documents.OpenAsync(Path.Combine(currentSolutionPath, filesChanged[i]));
                }
            }
        }
    }
}
