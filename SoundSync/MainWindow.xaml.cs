using Microsoft.Win32;
using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Media;

namespace SoundSync {
	public partial class MainWindow : Window {
		private MediaPlayer music;
		private DateTime start;
		private Timer timer;

		public MainWindow() {
			InitializeComponent();

			UpdateStatus("starting...");

			music = new MediaPlayer();
			music.MediaEnded += Music_MediaEnded;
			timer = new Timer();
			timer.AutoReset = true;
			timer.Elapsed += Timer_Elapsed;
			timer.Stop();

			//prepare UI elements
			WaitButton.IsEnabled = false;
			StopButton.IsEnabled = false;
			for (int i = 0; i <= 12; i++)
				Hour.Items.Add(i);
			for (int i = 0; i <= 59; i++)
				Minute.Items.Add(i);
			DateTime now = GetDateTime();
			Date.SelectedDate = now.Date;
			if (now.Hour < 12) {
				Hour.SelectedValue = now.Hour;
				AM.IsChecked = true;
			} else {
				Hour.SelectedValue = now.Hour - 12;
				PM.IsChecked = true;
			}
			Minute.SelectedValue = now.Minute;

			UpdateStatus("idle");
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e) {     //TODO: why is this not working? maybe because of threads or something?
			DateTime now = GetDateTime();
			int seconds = now.Second;
			Output.Text = seconds.ToString();
			if (seconds == 0)
				// start the music!
				music.Play();
			else if (seconds >= 59)
				timer.Interval = 1;
			else if (seconds >= 55)
				timer.Interval = 100;
			else if (seconds >= 45)
				timer.Interval = 1000;
			timer.Stop();
			if (seconds != 0)
				timer.Start();
		}

		private void Music_MediaEnded(object sender, EventArgs e) {
			LoadButton.IsEnabled = true;
			StopButton.IsEnabled = false;
			WaitButton.IsEnabled = true;
			UpdateStatus("idle");
		}

		private DateTime GetDateTime() {
			//get time and date from the Internet
			DateTime dateTime = DateTime.MinValue;
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://nist.time.gov/actualtime.cgi?lzbc=siqm9b");
			request.Method = "GET";
			request.Accept = "text/html, application/xhtml+xml, */*";
			request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
			request.ContentType = "application/x-www-form-urlencoded";
			request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore); //No caching
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			if (response.StatusCode == HttpStatusCode.OK) {
				StreamReader stream = new StreamReader(response.GetResponseStream());
				string html = stream.ReadToEnd();
				string time = Regex.Match(html, @"(?<=\btime="")[^""]*").Value;
				double milliseconds = Convert.ToInt64(time) / 1000.0;
				dateTime = new DateTime(1970, 1, 1).AddMilliseconds(milliseconds).ToLocalTime();
			}
			return dateTime;

			//this one kind of works but not always
			//return DateTime.ParseExact(WebRequest.Create("https://www.duckduckgo.com").GetResponse().Headers["date"], "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeUniversal);

		}

		private void UpdateStatus(string text) {
			Status.Text = "Status: " + text;
		}

		private void LoadButton_Click(object sender, RoutedEventArgs e) {
			UpdateStatus("loading file...");
			OpenFileDialog openFileDialog = new OpenFileDialog {
				Filter = "Sound Files|*.wav; *.mp3|All Files|*.*"
			};
			bool? hasFile = openFileDialog.ShowDialog();
			if (hasFile == true) {
				music.Open(new Uri(openFileDialog.FileName, UriKind.Absolute));
				UpdateStatus("loaded \"" + openFileDialog.SafeFileName + "\"");
				WaitButton.IsEnabled = true;
			} else
				UpdateStatus("idle");
		}

		private void WaitButton_Click(object sender, RoutedEventArgs e) {
			LoadButton.IsEnabled = false;
			StopButton.IsEnabled = true;
			WaitButton.IsEnabled = false;
			DateTime selectedDate = Date.SelectedDate.Value;
			start = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, (int)Hour.SelectedItem + (AM.IsChecked == true != ((int)Hour.SelectedItem == 12) ? 0 : 12), (int)Minute.SelectedItem, 0);
			timer.Interval = start.Subtract(GetDateTime()).TotalMilliseconds - 10000;
			timer.Start();
		}

		private void StopButton_Click(object sender, RoutedEventArgs e) {
			music.Stop();
			timer.Stop();
			LoadButton.IsEnabled = true;
			StopButton.IsEnabled = false;
			WaitButton.IsEnabled = true;
			UpdateStatus("idle");
		}
	}
}
