using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.IO;
using HtmlAgilityPack;
using System.Collections.Generic;
using VersOne.Epub;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Core;

namespace Epub_to_bionic
{

    public sealed partial class MainWindow : Window
    {
        private string convertedEpubPath;  // Class-level variable to store the converted file path
        private CoreCursor _handCursor;
        private CoreCursor _arrowCursor;

        public MainWindow()
        {
            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 1350));  // Set window size pro
            this.InitializeComponent();
            //Initialize cursors
            // Create hand and arrow cursors
            _handCursor = new CoreCursor(CoreCursorType.Hand, 1);   // Hand cursor
            _arrowCursor = new CoreCursor(CoreCursorType.Arrow, 1); // Default arrow cursor
        }

        // Event handler for the website link
        private void WebsiteLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://padled.net") { UseShellExecute = true });
        }

        // Event handler for the Buy Me a Coffee link
        private void BuyMeCoffeeLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/paddymac") { UseShellExecute = true });
        }

        private void OpenGitHubRepo_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Padled/SpeedRead") { UseShellExecute = true });
        }

        // Show the instructions when the ToggleButton is checked
        private void InstructionsToggle_Checked(object sender, RoutedEventArgs e)
        {
            instructionsPanel.Visibility = Visibility.Visible;
        }

        // Hide the instructions when the ToggleButton is unchecked
        private void InstructionsToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            instructionsPanel.Visibility = Visibility.Collapsed;
        }


        // Event handler for pointer entering the button (mouse hover)
        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Safely change the cursor to Hand (pointing hand)
            var coreWindow = Microsoft.UI.Xaml.Window.Current?.CoreWindow;
            if (coreWindow != null)
            {
                coreWindow.PointerCursor = _handCursor;
            }
        }

        // Event handler for pointer exiting the button (mouse leave)
        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Safely change the cursor back to Arrow (default)
            var coreWindow = Microsoft.UI.Xaml.Window.Current?.CoreWindow;
            if (coreWindow != null)
            {
                coreWindow.PointerCursor = _arrowCursor;
            }
        }


        // Browse button click event to select EPUB file
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".epub");

            // Ensure the picker works in desktop mode
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                // Display the selected file path in the TextBox
                filePathTextBox.Text = file.Path;

                // Enable the Convert Now button
                convertButton.IsEnabled = true;
            }
            else
            {
                // Disable the Convert Now button if no file is selected
                convertButton.IsEnabled = false;
            }
        }


        // Convert button click event to trigger EPUB to Bionic Reading conversion
        private async void ConvertButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            string filePath = filePathTextBox.Text;

            // Show loader and disable buttons during conversion
            conversionProgressRing.IsActive = true;
            conversionProgressRing.Visibility = Visibility.Visible;
            convertButton.IsEnabled = false;
            browseButton.IsEnabled = false;

            // Ensure the UI updates before starting the conversion
            await Task.Delay(100);  // Allow UI to refresh before starting the conversion

            // Check if the file exists
            if (File.Exists(filePath))
            {
                try
                {
                    await ConvertEpubToBionicAsync(filePath); // Call your conversion logic
                }
                finally
                {
                    // Hide loader and enable the browse button after conversion
                    conversionProgressRing.IsActive = false;
                    conversionProgressRing.Visibility = Visibility.Collapsed;
                    browseButton.IsEnabled = true;

                    // Show after-conversion options (Locate EPUB) and disable Convert button
                    locateEpubButton.Visibility = Visibility.Visible;
                    convertButton.IsEnabled = false;
                }
            }
            else
            {
                conversionProgressRing.IsActive = false;
                conversionProgressRing.Visibility = Visibility.Collapsed;

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Please select a valid EPUB file.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();

                // Re-enable the Convert button and hide the Locate EPUB button if needed
                convertButton.IsEnabled = true;
                locateEpubButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void LocateEpubButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(convertedEpubPath))
            {
                Process.Start("explorer.exe", $"/select,\"{convertedEpubPath}\"");
            }
            else
            {
                ContentDialog fileNotFoundDialog = new ContentDialog
                {
                    Title = "File Not Found",
                    Content = "The EPUB file could not be found at the specified path.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await fileNotFoundDialog.ShowAsync();
            }
        }

       
        private async Task ConvertEpubToBionicAsync(string filePath)
        {
            try
            {
                // Read the EPUB file using VersOne.Epub
                EpubBook epubBook = EpubReader.ReadBook(filePath);

                // List to store new chapters with modified content
                var modifiedChapters = new List<string>();

                // Process each chapter for Bionic Reading
                foreach (var chapter in epubBook.ReadingOrder)
                {
                    if (chapter is EpubLocalTextContentFile textContentFile && !string.IsNullOrEmpty(textContentFile.Content))
                    {
                        // Apply Bionic Reading to the chapter content
                        string modifiedContent = ApplyBionicReading(textContentFile.Content);

                        // Add the modified content to the new chapters list (or write it to a new location)
                        modifiedChapters.Add(modifiedContent);
                    }
                }

                // Save the modified EPUB (in a real implementation, you'd need to package it properly)
                string newFilePath = Path.Combine(Path.GetDirectoryName(filePath),
                                                  Path.GetFileNameWithoutExtension(filePath) + "_bionic.epub");

                await SaveModifiedEpubAsync(modifiedChapters, newFilePath);  // This needs implementation

                // Store the converted file path
                convertedEpubPath = newFilePath;

            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"An error occurred during conversion: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot  // Ensure the ContentDialog appears properly

                };
                await errorDialog.ShowAsync();
            }
        }

        // Apply Bionic Reading transformation
        private string ApplyBionicReading(string content)
        {
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(content);

            // Iterate through each text node and apply Bionic Reading transformation
            foreach (var textNode in document.DocumentNode.SelectNodes("//text()"))
            {
                if (!string.IsNullOrEmpty(textNode.InnerText.Trim()))
                {
                    string processedText = TransformToBionicReading(textNode.InnerText);
                    textNode.InnerHtml = processedText;
                }
            }

            return document.DocumentNode.OuterHtml; // Return the processed HTML
        }

        // Function to transform text for Bionic Reading
        private string TransformToBionicReading(string text)
        {
            string[] words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length > 2)
                {
                    int boldLength = Math.Min(word.Length / 3, 3); // Bold the first 1/3rd of the word, up to 3 characters
                    words[i] = $"<b>{word.Substring(0, boldLength)}</b>{word.Substring(boldLength)}";
                }
            }
            return string.Join(" ", words);
        }

        //Will open this again in future, have some issues delivering from setup email, better to just use send to kindle program
        //private async void SendToKindleButton_Click(object sender, RoutedEventArgs e)
        //{
        //    string kindleEmail = kindleEmailTextBox.Text;  // Get Kindle email from text box
        //    if (string.IsNullOrEmpty(kindleEmail) || !kindleEmail.EndsWith("@kindle.com"))
        //    {
        //        ContentDialog invalidEmailDialog = new ContentDialog
        //        {
        //            Title = "Invalid Email",
        //            Content = "Please enter a valid Kindle email address (ending with @kindle.com).",
        //            CloseButtonText = "OK",
        //            XamlRoot = this.Content.XamlRoot
        //        };
        //        await invalidEmailDialog.ShowAsync();
        //        return;
        //    }

        //    try
        //    {
        //        using (MailMessage mailMessage = new MailMessage("epub2bionic@gmail.com", kindleEmail))
        //        {
        //            //mailMessage.Subject = "Your EPUB File";
        //            //mailMessage.Body = "Here is your converted EPUB file.";

        //            // Ensure the file exists and attach the file
        //            if (File.Exists(convertedEpubPath))
        //            {
        //                mailMessage.Attachments.Add(new Attachment(convertedEpubPath));
        //            }
        //            else
        //            {
        //                ContentDialog fileNotFoundDialog = new ContentDialog
        //                {
        //                    Title = "File Not Found",
        //                    Content = $"The converted EPUB file could not be found at {convertedEpubPath}.",
        //                    CloseButtonText = "OK",
        //                    XamlRoot = this.Content.XamlRoot
        //                };
        //                await fileNotFoundDialog.ShowAsync();
        //                return;
        //            }

        //            // Set up the SMTP client
        //            using (SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587))
        //            {
        //                smtpClient.Credentials = new NetworkCredential("epub2bionic@gmail.com", "");
        //                smtpClient.EnableSsl = true;

        //                // Send the email asynchronously
        //                await smtpClient.SendMailAsync(mailMessage);
        //            }

        //            ContentDialog successDialog = new ContentDialog
        //            {
        //                Title = "Email Sent",
        //                Content = $"The EPUB file has been sent to {kindleEmail}.",
        //                CloseButtonText = "OK",
        //                XamlRoot = this.Content.XamlRoot
        //            };
        //            await successDialog.ShowAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ContentDialog errorDialog = new ContentDialog
        //        {
        //            Title = "Error",
        //            Content = $"An error occurred while sending the email: {ex.Message}",
        //            CloseButtonText = "OK",
        //            XamlRoot = this.Content.XamlRoot
        //        };
        //        await errorDialog.ShowAsync();
        //    }
        //}


        // Helper function to find the correct folder that contains chapter files (HTML/XHTML/XML)
        private string FindTextFolder(string rootDirectory)
        {
            // Common EPUB folder names that might contain the text files
            var possibleTextFolders = new[] { "text", "OEBPS", "OPS" };

            // Check if any of the common folders exist
            foreach (var folder in possibleTextFolders)
            {
                string folderPath = Path.Combine(rootDirectory, folder);
                if (Directory.Exists(folderPath))
                {
                    return folderPath;
                }
            }

            // If no common folder is found, look for any folder with HTML/XHTML files
            var subdirectories = Directory.GetDirectories(rootDirectory, "*", SearchOption.AllDirectories);
            foreach (var directory in subdirectories)
            {
                if (Directory.GetFiles(directory, "*.xhtml", SearchOption.TopDirectoryOnly).Any() ||
                    Directory.GetFiles(directory, "*.html", SearchOption.TopDirectoryOnly).Any())
                {
                    return directory;
                }
            }

            // Return null if no suitable folder is found
            return null;
        }

        private async Task SaveModifiedEpubAsync(List<string> modifiedChapters, string newEpubFilePath)
        {
            try
            {
                // 1. Create a temporary directory to store the extracted EPUB contents
                string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);

                // 2. Extract the original EPUB contents to the temporary directory
                string originalEpubFilePath = filePathTextBox.Text;  // The original EPUB path
                ZipFile.ExtractToDirectory(originalEpubFilePath, tempDirectory);

                // Log all files in the extracted EPUB directory
                Debug.WriteLine("All files in the extracted EPUB directory:");
                foreach (var file in Directory.GetFiles(tempDirectory, "*.*", SearchOption.AllDirectories))
                {
                    Debug.WriteLine(file);
                }

                // **Find the text folder dynamically**
                string textFolderPath = FindTextFolder(tempDirectory);
                if (textFolderPath != null)
                {
                    // Search for HTML, XHTML, and XML files in the located folder
                    string[] chapterFiles = Directory.GetFiles(textFolderPath, "*.*", SearchOption.AllDirectories)
                        .Where(file => file.EndsWith(".html") || file.EndsWith(".xhtml") || file.EndsWith(".xml"))
                        .ToArray();

                    // **Handle the title page if necessary (could be XHTML or another format)**
                    string titlePagePath = Path.Combine(tempDirectory, "titlepage.xhtml");  // Adjust this path if needed
                    bool hasTitlePage = File.Exists(titlePagePath);

                    // Log the found chapter files
                    Debug.WriteLine("Found Chapter Files:");
                    foreach (var file in chapterFiles)
                    {
                        Debug.WriteLine(file);
                    }

                    // If no valid chapter files were found, throw an exception
                    if (chapterFiles.Length == 0 && !hasTitlePage)
                    {
                        throw new Exception("No valid chapter files (.html, .xhtml, .xml) found in the EPUB structure.");
                    }

                    // If there's a title page, handle it separately
                    if (hasTitlePage)
                    {
                        Debug.WriteLine("Found title page: " + titlePagePath);
                    }

                    // Log counts for chapters and modified content
                    Debug.WriteLine($"Number of modified chapters: {modifiedChapters.Count}");
                    Debug.WriteLine($"Number of chapter files: {chapterFiles.Length}");

                    int totalFiles = chapterFiles.Length + (hasTitlePage ? 1 : 0);  // Add title page if it exists
                    int difference = totalFiles - modifiedChapters.Count;  // Calculate the difference between files and modified chapters

                    if (totalFiles >= modifiedChapters.Count)  // Ensure there are enough files to skip
                    {
                        // Replace the chapter content, skipping the first 'difference' number of files
                        for (int i = difference; i < chapterFiles.Length; i++)
                        {
                            // 4. Replace the content of each chapter file with the modified content
                            File.WriteAllText(chapterFiles[i], modifiedChapters[i - difference]);  // Adjust the index by 'difference'
                        }

                        // Handle the title page if necessary
                        if (hasTitlePage && modifiedChapters.Count > chapterFiles.Length)
                        {
                            // Replace the title page content with the first modified chapter (adjust as necessary)
                            File.WriteAllText(titlePagePath, modifiedChapters[chapterFiles.Length - difference]);
                        }
                    }
                    else
                    {
                        // Log detailed mismatch information
                        Debug.WriteLine("Mismatch between original chapter files and modified chapters.");
                        Debug.WriteLine($"Chapter Files ({chapterFiles.Length}):");
                        foreach (var file in chapterFiles)
                        {
                            Debug.WriteLine(file);
                        }
                        Debug.WriteLine($"Modified Chapters ({modifiedChapters.Count}):");
                        throw new Exception("Mismatch between original chapters and modified chapters.");
                    }

                }
                else
                {
                    throw new Exception("Text folder not found in the EPUB structure.");
                }

                // 5. Repackage the temporary folder back into an EPUB file
                string tempEpubFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(newEpubFilePath));

                if (File.Exists(tempEpubFilePath))
                {
                    File.Delete(tempEpubFilePath);  // Delete the file if it already exists
                }

                ZipFile.CreateFromDirectory(tempDirectory, tempEpubFilePath);

                // 6. Move the newly created EPUB file to the desired location
                File.Move(tempEpubFilePath, newEpubFilePath, true);

                // 7. Clean up the temporary directory
                Directory.Delete(tempDirectory, true);

                // Show success message
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Conversion Complete",
                    Content = $"EPUB has been converted and saved to {newEpubFilePath}.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot  // Set the XamlRoot to the current window's XamlRoot
                };
                await dialog.ShowAsync();  // Ensure you use await for ShowAsync
            }
            catch (Exception ex)
            {
                // Log the exception and show an error dialog
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error During Conversion",
                    Content = $"An error occurred: {ex.Message}\n\n{ex.StackTrace}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot  // Set the XamlRoot to the current window's XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

    }
}
