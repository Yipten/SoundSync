using GuerrillaNtp;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Media;

namespace SoundSync {
	public partial class MainWindow : Window {
		private readonly MediaPlayer music;
		private readonly Timer timer;

		private DateTime start;

		private string fileName;

		public MainWindow() {
			InitializeComponent();

			UpdateStatus("starting...");

			music = new MediaPlayer();
			music.MediaEnded += Music_MediaEnded;
			timer = new Timer();
			timer.AutoReset = false;
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

		private void Music_MediaEnded(object sender, EventArgs e) {
			//reset MediaPlayer and buttons after music ends
			music.Stop();
			LoadButton.IsEnabled = true;
			StopButton.IsEnabled = false;
			WaitButton.IsEnabled = true;
			Date.IsEnabled = true;
			Hour.IsEnabled = true;
			Minute.IsEnabled = true;
			AM.IsEnabled = true;
			PM.IsEnabled = true;
			UpdateStatus("\"" + fileName + "\" ended");
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
			//stop the timer
			timer.Stop();
			//start the music!
			Dispatcher.Invoke(() => {
				music.Play();
				UpdateStatus("playing \"" + fileName + "\"");
			});
		}

		private DateTime GetDateTime() {
			//the method to the madness
			int method = 1;
			//DateTime that will be returned
			DateTime dateTime = DateTime.Now;
			//Stopwatch to account for delay in getting DateTime from the Internet
			Stopwatch stopwatch = new Stopwatch();
			switch (method) {
				case 0:
					//get time and date from time.gov
					try {
						StreamReader stream = new StreamReader(WebRequest.Create("https://nist.time.gov/actualtime.cgi?lzbc=siqm9b").GetResponse().GetResponseStream());
						string html = stream.ReadToEnd();
						string time = Regex.Match(html, @"(?<=\btime="")[^""]*").Value;
						double milliseconds = Convert.ToInt64(time) / 1000;
						dateTime = new DateTime(1970, 1, 1).AddMilliseconds(milliseconds).ToLocalTime();
					} catch {
						MessageBox.Show("No Internet connection. Local time will be used (not recommended).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					break;
				case 1:
					//get time from website header
					try {
						dateTime = DateTime.ParseExact(WebRequest.Create("https://www.duckduckgo.com").GetResponse().Headers["date"], "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeUniversal);
					} catch {
						MessageBox.Show("No Internet connection. Local time will be used (not recommended).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					break;
				case 2:         //TODO: figure out why it sometimes throws NtpException
								//get time from Network Time Protocol (currently most reliable)
					try {
						NtpClient ntp = new NtpClient(Dns.GetHostAddresses("pool.ntp.org")[0]) {
							Timeout = new TimeSpan(0, 0, 10)
						};
						NtpPacket packet = ntp.Query();
						stopwatch.Start();
						ntp.Dispose();
						TimeSpan offset = packet.CorrectionOffset + packet.RoundTripTime;
						dateTime = DateTime.Now + offset;
					} catch (NtpException) {
						MessageBox.Show("An error occurred while fetching the NTP packet.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					} catch (SocketException) {
						MessageBox.Show("SocketException - timeout exceeded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					//} catch {
					//	MessageBox.Show("No Internet connection. Local time will be used (not recommended).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					//}
					break;
			}
			stopwatch.Stop();
			return dateTime.AddMilliseconds(stopwatch.ElapsedMilliseconds);
		}

		private void UpdateStatus(string text) {
			//change text displayed in status text box for diagnostics and to inform the user
			Status.Text = "Status: " + text;
		}

		private void LoadButton_Click(object sender, RoutedEventArgs e) {
			UpdateStatus("loading file...");
			OpenFileDialog openFileDialog = new OpenFileDialog {
				Filter = "Sound Files|*.wav; *.mp3|All Files|*.*"
			};
			//if a file has been selected by the user, load it into the MediaPlayer
			if (openFileDialog.ShowDialog() == true) {
				music.Open(new Uri(openFileDialog.FileName, UriKind.Absolute));
				WaitButton.IsEnabled = true;
				fileName = openFileDialog.SafeFileName;
				UpdateStatus("loaded \"" + fileName + "\"");
			} else
				UpdateStatus("idle");
		}

		private void WaitButton_Click(object sender, RoutedEventArgs e) {
			//get date and time from selected values
			DateTime selectedDate = Date.SelectedDate.Value;
			start = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, (int)Hour.SelectedItem + (AM.IsChecked == true != ((int)Hour.SelectedItem == 12) ? 0 : 12), (int)Minute.SelectedItem, 0, 0);
			if (start.CompareTo(GetDateTime()) > 0) {
				LoadButton.IsEnabled = false;
				StopButton.IsEnabled = true;
				WaitButton.IsEnabled = false;
				Date.IsEnabled = false;
				Hour.IsEnabled = false;
				Minute.IsEnabled = false;
				AM.IsEnabled = false;
				PM.IsEnabled = false;
				//set timer interval as time until start time occurs
				timer.Interval = start.Subtract(GetDateTime()).TotalMilliseconds;
				timer.Start();
				UpdateStatus("starting at " + start.ToShortTimeString() + " on " + start.ToShortDateString());
			} else {
				MessageBox.Show("Cannot set to a time in the past.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void StopButton_Click(object sender, RoutedEventArgs e) {
			//stop music and timer
			music.Stop();
			timer.Stop();
			LoadButton.IsEnabled = true;
			StopButton.IsEnabled = false;
			WaitButton.IsEnabled = true;
			Date.IsEnabled = true;
			Hour.IsEnabled = true;
			Minute.IsEnabled = true;
			AM.IsEnabled = true;
			PM.IsEnabled = true;
			UpdateStatus("loaded \"" + fileName + "\"");
		}
	}
}