using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Shell;
using System.IO;
using DandC;

namespace WpfApplication1
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {

    protected bool isRunning = false;
    protected string directory = Environment.CurrentDirectory;
    protected int currentStage = 0;
    protected bool audioDownloadStage = false;

    protected Process currentTask = null;
    protected bool currentTaskRunning = false;

    protected string downloadFile = "";
    protected int audioLength = 0;

    public MainWindow()
    {
      InitializeComponent();
    }

    private void resetStages()
    {
      if (currentTaskRunning)
      {
        currentTask.Kill();
        currentTaskRunning = false;
      }
      TaskbarItemInfo.ProgressValue = 0.0;
      TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
      taskText.Content = "Ready";
      taskDetail.Content = "";
      taskProgress.Value = 0.0;
      overallDetail.Content = "";
      overallProgress.Value = 0.0;
      urlBox.IsEnabled = true;
      currentStage = 0;
      optionsButton.IsEnabled = true;
      optionsButton.Opacity = 1.0;
      startButton.Content = "Start";
      isRunning = false;
    }

    private void startButton_Click(object sender, RoutedEventArgs e)
    {

      if (isRunning) // Cancel task
      {
        resetStages();
      }

      else // Start task
      {
        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
        startTasks();
        overallDetail.Content = "0/1";
        optionsButton.IsEnabled = false;
        optionsButton.Opacity = 0.5;
        startButton.Content = "Cancel";
        isRunning = true;
      }
    }

    private void urlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
      urlHint.Opacity = urlBox.Text.Length > 0 ? 0.0 : 1.0;
    }

    private void startDownloadTask(string link)
    {

      currentTask = new Process();
      currentTask.StartInfo.RedirectStandardOutput = true;
      currentTask.StartInfo.RedirectStandardError = true;
      currentTask.StartInfo.UseShellExecute = false;
      currentTask.StartInfo.CreateNoWindow = true;
      currentTask.EnableRaisingEvents = true;
      currentTask.Exited += new EventHandler(taskFinishedHandler);
      audioDownloadStage = false;
      string fileLocation = directory + "\\youtube-dl.exe";
      string outputDirectory = directory + "\\processing";
      string arguments = "--no-check-certificate --newline --write-info-json -k --output \"" + outputDirectory + "\\original.mp4\" " + link;
      Console.WriteLine("File location: " + fileLocation);
      Console.WriteLine("Arguments: " + arguments);
      currentTask.StartInfo.FileName = fileLocation;
      currentTask.StartInfo.Arguments = arguments;

      currentTask.OutputDataReceived += new DataReceivedEventHandler(
        (s, output) =>
        {
          Console.WriteLine(output.Data);

          if (!Dispatcher.CheckAccess())
          {
            string text = output.Data;
            if (text != null)
            {
              // TODO: Change the checks to use the split array instead
              string[] splitData = text.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

              if (text.StartsWith("[download]  ")) // Download update
              {
                Dispatcher.Invoke(() => taskText.Content = "Downloading...", DispatcherPriority.Normal);
                string pre = splitData[1];
                string stringPercentage = pre.Substring(0, pre.Length - 1);
                double percentage = 0.0;
                try
                {
                  percentage = Convert.ToDouble(stringPercentage);
                }
                catch
                {
                  return;
                }
                string rate = splitData[5];
                Dispatcher.Invoke(() => taskDetail.Content = String.Format("{0} ({1})", splitData[5], splitData[3]), DispatcherPriority.Normal);
                if (audioDownloadStage)
                {
                  Dispatcher.Invoke(() => taskText.Content = "Downloading audio...", DispatcherPriority.Normal);
                }
                Dispatcher.Invoke(() => taskProgress.Value = percentage, DispatcherPriority.Normal);
              }

              else if (text.StartsWith("[download] 1")) // Download done
              {
                audioDownloadStage = true;
                Dispatcher.Invoke(() => taskDetail.Content = "(" + splitData[3] + ")", DispatcherPriority.Normal);
                Dispatcher.Invoke(() => taskProgress.Value = 100.0, DispatcherPriority.Normal);
              }

              else if (text.StartsWith("[download] Destination: "))
              {
                Console.WriteLine("Here is the current line: " + text);
                downloadFile = "\"" + text.Substring(24) + "\"";
                Console.WriteLine("*** Destination file: " + downloadFile);
              }

            }
          }
        }
      );
      currentTask.ErrorDataReceived += new DataReceivedEventHandler(
        (s, output) =>
        {
          Console.WriteLine(output.Data);
        }
      );

      currentTask.Start();
      currentTaskRunning = true;

      currentTask.BeginErrorReadLine();
      currentTask.BeginOutputReadLine();
      Console.WriteLine("Stuff finished.");
    }

    private void taskFinishedHandler(object sender, EventArgs e)
    {
      currentTaskRunning = false;
      string taskType = currentStage == 0 ? "downloader" : "converter";
      Console.WriteLine("The " + taskType + " has finished.");
      if (currentTask.ExitCode != 0)
      {
        Console.WriteLine("There was an error: " + currentTask.ExitCode);
        currentStage = 0;
        if (currentTask.ExitCode > 0)
        {
          Dispatcher.Invoke(() => TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error, DispatcherPriority.Normal);
          Dispatcher.Invoke(() => MessageBox.Show(Application.Current.MainWindow, "The " + taskType + " hit a snag! Check the URL and try again.", "Aw, shucks!"), DispatcherPriority.Normal);
          Dispatcher.Invoke(() => resetStages(), DispatcherPriority.Normal);
        }
      }
      else
      {
        currentStage++;
        startTasks();
      }
    }

    // Converts the file located in the processing folder
    private void startConvertTask(int duration)
    {
      Console.WriteLine("About to start conversion.");
      currentTask = new Process();
      currentTask.StartInfo.RedirectStandardOutput = true;
      currentTask.StartInfo.RedirectStandardError = true;
      currentTask.StartInfo.UseShellExecute = false;
      currentTask.StartInfo.CreateNoWindow = true;
      currentTask.EnableRaisingEvents = true;
      currentTask.Exited += new EventHandler(taskFinishedHandler);
      string fileLocation = directory + "\\ffmpeg.exe";
      string outputDirectory = directory + "\\processing";
      string arguments = "-y -i " + downloadFile + " -ac 1 -qscale:a 8 \"" + outputDirectory + "\\converted.mp3\" ";
      Console.WriteLine("File location: " + fileLocation);
      Console.WriteLine("Arguments: " + arguments);
      currentTask.StartInfo.FileName = fileLocation;
      currentTask.StartInfo.Arguments = arguments;

      currentTask.ErrorDataReceived += new DataReceivedEventHandler(
        (s, output) =>
        {
          Console.WriteLine(output.Data);

          if (!Dispatcher.CheckAccess())
          {
            string text = output.Data;
            if (text != null)
            {
              if (text.StartsWith("size=")) // Convert update
              {
                Dispatcher.Invoke(() => taskText.Content = "Converting...", DispatcherPriority.Normal);

                int hours, minutes, seconds;
                int.TryParse(text.Substring(21, 2), out hours);
                int.TryParse(text.Substring(24, 2), out minutes);
                int.TryParse(text.Substring(27, 2), out seconds);
                int currentTime = hours * 3600 + minutes * 60 + seconds;
                double percentage = 0.0;
                if (duration == 0)
                {
                  percentage = 100.0;
                }
                else
                {
                  percentage = ((double)currentTime / (double)duration) * 100.0;
                }
                string bitrate = text.Substring(41, 13).Trim();

                Dispatcher.Invoke(() => taskProgress.Value = percentage, DispatcherPriority.Normal);
                Dispatcher.Invoke(() => taskDetail.Content = bitrate, DispatcherPriority.Normal);
              }

            }
          }
        }
      );
      currentTask.OutputDataReceived += new DataReceivedEventHandler(
        (s, output) =>
        {
          Console.WriteLine(output.Data);
        }
      );

      currentTask.Start();
      currentTaskRunning = true;

      currentTask.BeginErrorReadLine();
      currentTask.BeginOutputReadLine();
      Console.WriteLine("Conversion finished.");
    }

    // Starts the next task based on currentStage
    // This also sets up the content
    private void startTasks()
    {

      // Start the next task
      string processingDirectory = directory + "\\processing";
      System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(processingDirectory);
      switch (currentStage) {

        case 0: // Start new download

          // Delete all of the contents in the folder
          foreach (System.IO.FileInfo file in directoryInfo.GetFiles())
          {
            file.Delete();
          }

          // Set content up
          urlBox.IsEnabled = false;
          taskText.Content = "Checking URL...";
          string link = urlBox.Text;
          if (link.Length == 0)
          {
            link = "http://www.voachinese.com/";
          }
          startDownloadTask(link);
          break;

        case 1: // Convert

          Process durationTask = new Process();
          durationTask.StartInfo.RedirectStandardOutput = true;
          durationTask.StartInfo.UseShellExecute = false;
          durationTask.StartInfo.CreateNoWindow = true;
          string fileLocation = directory + "\\ffprobe.exe";
          string arguments = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 " + downloadFile;
          Console.WriteLine("File location: " + fileLocation);
          Console.WriteLine("Arguments: " + arguments);
          durationTask.StartInfo.FileName = fileLocation;
          durationTask.StartInfo.Arguments = arguments;
          string output = "0";
          try
          {
            durationTask.Start();
            output = durationTask.StandardOutput.ReadToEnd().Trim();
            durationTask.WaitForExit();
          }
          catch (Exception e)
          {
          Console.WriteLine("{0}\nException caught.", e);
          Console.WriteLine("Warning: ffprobe not found.");
          }

          double duration = 0;
          double.TryParse(output, out duration);
          startConvertTask((int)duration);
          break;

        case 2: // Finished!
          Dispatcher.Invoke(() => TaskbarItemInfo.ProgressValue = 1.0, DispatcherPriority.Normal);
          Dispatcher.Invoke(() => overallProgress.Value = 100.0, DispatcherPriority.Normal);
          Dispatcher.Invoke(() => overallDetail.Content = "1/1", DispatcherPriority.Normal);
          Dispatcher.Invoke(() => MessageBox.Show(Application.Current.MainWindow, "Your download and conversion has finished.", "All done!"), DispatcherPriority.Normal);
          Dispatcher.Invoke(() => resetStages(), DispatcherPriority.Normal);
          break;

        default:
          Console.WriteLine("Current stage is out of bounds.");
          return;

      }
    }

    private void optionsButton_Click(object sender, RoutedEventArgs e)
    {
      OptionsWindow options = new OptionsWindow();
      options.ShowDialog();
      //this.IsEnabled = false;
      //MessageBox.Show("This feature is not implemented yet. Sorry.", "Darn");
    }

    private void openButton_Click(object sender, RoutedEventArgs e)
    {
      Console.WriteLine(directory);
      System.Diagnostics.Process.Start("explorer.exe", "\"" + directory + "\\processing\"");
    }

    private void taskProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      switch (currentStage)
      {
        case 0:
          if (!audioDownloadStage) {
            overallProgress.Value = taskProgress.Value * 0.85;
          }
          break;

        case 1:
          overallProgress.Value = 85 + taskProgress.Value * 0.15;
          break;

        default:
          return;
      }

      TaskbarItemInfo.ProgressValue = overallProgress.Value / 100.0;
    }
  }
}
