﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PRFileOpener.Options;

namespace PRFileOpener.Commands
{
    [Command(PackageIds.OpenFilesCommand)]
    internal sealed class OpenFilesCommand : BaseCommand<OpenFilesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var currentSolution = await VS.Solutions.GetCurrentSolutionAsync();
            var currentSolutionPath = Directory.GetParent(currentSolution.FullPath).FullName;

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

            var sp = new Stopwatch();
            sp.Start();
            var files = filesChanged.Select(f => $"{currentSolutionPath}\\{f.Replace("/", "\\")}");
            var tasks = files.Select(f => VS.Documents.OpenAsync(f)).ToArray();
            await Task.WhenAll(tasks);
            sp.Stop();
            Debug.WriteLine($"Elapsed ms was : {sp.ElapsedMilliseconds}");
        }
    }
}