﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Windows.Devices.Geolocation;
using System.Windows.Threading;
using Windows.UI.Core;
using Coding4Fun.Toolkit.Controls;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Telegram.Core.Logging;
using Telegram.Model;
using Telegram.Model.Wrappers;
using Telegram.MTProto;
using Telegram.UI.Controls;
using Telegram.Utils;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;
using Logger = Telegram.Core.Logging.Logger;

namespace Telegram.UI {
    public partial class DialogPage : PhoneApplicationPage {
        private static readonly Logger logger = LoggerFactory.getLogger(typeof(DialogListControl));
        private TelegramSession session;

        private bool keyboardWasShownBeforeEmojiPanelIsAppeared;

        private DialogModel model = null;
        private volatile bool needDialogCreate = false;



        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            string uriParam = "";

//            if (NavigationContext.QueryString.TryGetValue("modelId", out uriParam)) {
//                model = TelegramSession.Instance.Dialogs.Model.Dialogs[(int.Parse(uriParam))];
//
////                PhoneApplicationService.Current.State["MapMedia"] = geoMedia;
////                NavigationService.Navigate(new Uri("/UI/Pages/DialogPage.xaml?modelId=" + returnToModelId + "&action=sendMedia&content=MapMedia", UriKind.Relative));
//                if (NavigationContext.QueryString.TryGetValue("action", out uriParam)) {
//                    string action = uriParam;
//                    
//                    NavigationContext.QueryString.TryGetValue("content", out uriParam);
//                    string content = uriParam;
//
//                    DoAction(action, content);
//                }
//            } else
//            if (NavigationContext.QueryString.TryGetValue("userId", out uriParam))
//            {
//                int userId = int.Parse(uriParam);
//                var targetPeer = TL.peerUser(userId);
//
//                foreach (DialogModel dialogModel in TelegramSession.Instance.Dialogs.Model.Dialogs)
//                {
//                    if (dialogModel is DialogModelEncrypted) continue;
//
//                    if (TLStuff.PeerEquals(dialogModel.Peer, targetPeer))
//                    {
//                        model = dialogModel;
//                        break;
//                    }
//                }
//
//                if (model == null)
//                {
//                    model = new DialogModelPlain(TL.peerUser(userId), TelegramSession.Instance);
//                    needDialogCreate = true;
//                }
//            }

            //TelegramSession.Instance.Dialogs.OpenedDialog = model;

            ///UpdateDataContext();

            // init notice
            // FIXME: assure that no actual history received from server
            // or this is new chat
            if (MessageLongListSelector.ItemsSource == null || MessageLongListSelector.ItemsSource.Count == 0)
                ShowNotice();
        }

        private void DoAction(string action, string content) {
            if (!(model is DialogModelPlain))
                return;

            if (action == "sendMedia") {
                InputMedia media = (InputMedia) PhoneApplicationService.Current.State[content];
                ((DialogModelPlain)model).SendMedia(media);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            base.OnNavigatingFrom(e);
            if (EmojiPopup.IsOpen) {
                ToggleEmoji();
            }

            if (AttachPopup.IsOpen) {
                ToggleAttach();
            }

            lock (typingLock) {
                if (typing) {
                    timer.Stop();
                    model.SendTyping(false);
                }
            }
        }

        protected override void OnBackKeyPress(CancelEventArgs e) {
            if (EmojiPopup.IsOpen) {
                ToggleEmoji();
                e.Cancel = true;
                return;
            }

            if (AttachPopup.IsOpen) {
                ToggleAttach();
                e.Cancel = true;
                return;
            }

            lock(typingLock) {
                if(typing) {
                    timer.Stop();
                    model.SendTyping(false);
                }
            }

            NavigationService.Navigate(new Uri("/UI/Pages/StartPage.xaml", UriKind.Relative));
        }

        private void UpdateDataContext() {
            this.DataContext = model;
            MessageLongListSelector.ItemsSource = model.Messages;
            MessageLongListSelector.Model = model;

            model.Messages.CollectionChanged += delegate {
                if (MessageLongListSelector.ItemsSource == null || MessageLongListSelector.ItemsSource.Count == 0)
                    ShowNotice();
                else
                    HideNotice();
            };

            if (model.IsWaiting) { 
                messageEditor.IsEnabled = false;
                messageEditor.Text = "Waiting for user...";
            }

            model.PropertyChanged += delegate(object sender, PropertyChangedEventArgs args) {
                if (args.PropertyName == "IsWaiting") {
                    messageEditor.IsEnabled = true;
                    messageEditor.Text = "";
                }
            };
        }

        public DialogPage() {

            session = TelegramSession.Instance;
            model = TelegramSession.Instance.Dialogs.OpenedDialog;
            
            

            InitializeComponent();

            UpdateDataContext();

            DisableEditBox();

            messageEditor.GotFocus += delegate {
                AttachPopup.IsOpen = false;
                EmojiPopup.IsOpen = false;
            };

            messageEditor.LostFocus += delegate {
                if (!EmojiPopup.IsOpen && !AttachPopup.IsOpen)
                    MainPanel.Margin = new Thickness(0, 0, 0, 0);
            };

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerOnTick;
            

            messageEditor.TextChanged += MessageEditorOnTextChanged;

            EmojiPanelControl.BackspaceClick += EmojiPanelControlOnBackspaceClick;
            EmojiPanelControl.KeyboardClick += EmojiPanelControlOnKeyboardClick;
            EmojiPanelControl.EmojiGridListSelector.SelectionChanged += EmojiGridListSelectorOnSelectionChanged;

        }

        private void TimerOnTick(object sender, EventArgs eventArgs) {
            lock(typingLock) {
                if(typing && DateTime.Now - lastTextChanged > TimeSpan.FromSeconds(5)) {
                    model.SendTyping(false);
                    typing = false;
                    timer.Stop();
                }
            }
        }

        private object typingLock = new object();
        private bool typing = false;
        private DateTime lastTextChangedSended = DateTime.Now - TimeSpan.FromDays(10);
        private DateTime lastTextChanged = DateTime.Now - TimeSpan.FromDays(10);
        private DispatcherTimer timer;
        private void MessageEditorOnTextChanged(object sender, TextChangedEventArgs textChangedEventArgs) {
            lock(typingLock) {
                if(typing == false) {
                    model.SendTyping(true);
                    lastTextChangedSended = DateTime.Now;
                    typing = true;
                    timer.Start();
                } else if(DateTime.Now - lastTextChangedSended > TimeSpan.FromSeconds(5)) {
                    model.SendTyping(true);
                    lastTextChangedSended = DateTime.Now;
                }
                lastTextChanged = DateTime.Now;
            }
        }

        private void EmojiPanelControlOnKeyboardClick(object sender, object args) {
            if (EmojiPopup.IsOpen)
                ToggleEmoji();

            messageEditor.Focus();
        }

        private void EmojiPanelControlOnBackspaceClick(object sender, object args) {
            if (messageEditor.Text.Length == 0)
                return;

            var utf32list = messageEditor.Text.ToUtf32().ToList();
            utf32list.RemoveAt(utf32list.Count-1);
            messageEditor.Text = new string(utf32list.ToUtf16().ToArray());
        }


        private void ShowNotice() {
            if (IsPrivate()) {
                SecretChatNoticeControl.Visibility = Visibility.Visible;
            } else {
                ChatNoticeControl.Visibility = Visibility.Visible;
            }
        }

        private void HideNotice() {
            SecretChatNoticeControl.Visibility = Visibility.Collapsed;
            ChatNoticeControl.Visibility = Visibility.Collapsed;
        }

        private bool IsPrivate() {
            return model is DialogModelEncrypted;
        }

        private void EmojiGridListSelectorOnSelectionChanged(object sender, SelectionChangedEventArgs selectionChangedEventArgs) {
            var selector = (LongListSelector) sender;
            if (selector.SelectedItem == null)
                return;
            var emoji = (EmojiItemModel)selector.SelectedItem;
            messageEditor.Text += emoji.ToString();
            selector.SelectedItem = null;
        }

        private void Dialog_Message_Send(object sender, EventArgs e) {
            var text = messageEditor.Text;
            messageEditor.Text = "";

            lock (typingLock) {
                if (typing) {
                    timer.Stop();
                    model.SendTyping(false);
                }
            }

            if (needDialogCreate) {
                model.SendMessage(text).ContinueWith((result) => {
                    if (result.Result) {
                        TelegramSession.Instance.Dialogs.Model.Dialogs.Add(model);
                        needDialogCreate = false;
                    }
                });
            } else { 
                model.SendMessage(text);
            }
        }

        private void PickAndSendPhoto(object sender, GestureEventArgs e) {
            var photo = new PhotoChooserTask { ShowCamera = true };
            photo.Completed += photoChooserTask_Completed;
            photo.Show();
        }

        private void PickAndSendVideo(object sender, GestureEventArgs e) {
            var photo = new PhotoChooserTask {  };
            photo.Completed += photoChooserTask_Completed;
            photo.Show();
        }

        void docChooserTask_Completed(object sender, PhotoResult e) {
            try {
                if (e.ChosenPhoto == null)
                    return;

                Task.Run(() => StartUploadPhoto(e.OriginalFileName, e.ChosenPhoto));

                if (AttachPopup.IsOpen)
                    ToggleAttach();

            } catch (Exception exception) {
                Debug.WriteLine("Exception in photoChooserTask_Completed " + exception.Message);
            }
        }

        private static void GetImageSizeAspect(int h, int w, out int nh, out int nw) {
            int longestSide = w > h ? w : h;

            float aspectRatio = longestSide / 800;

            nh = (int) (h / aspectRatio);
            nw = (int) (w / aspectRatio);
        }

        void photoChooserTask_Completed(object sender, PhotoResult e) {
            try {
                if (e.ChosenPhoto == null)
                    return;
                BitmapImage image = new BitmapImage();
                image.SetSource(e.ChosenPhoto);
                WriteableBitmap wb = new WriteableBitmap(image);
                int rw, rh;
                GetImageSizeAspect(image.PixelHeight, image.PixelWidth, out rh, out rw);

                MemoryStream ms = new MemoryStream();
                wb.SaveJpeg(ms, rw, rh, 0, 87);
                ms.Position = 0;

                Task.Run(() => {
                    StartUploadPhotoAndDispose(e.OriginalFileName, ms);
                });

                if (AttachPopup.IsOpen)
                    ToggleAttach();

            } catch (Exception exception) {
                Debug.WriteLine("Exception in photoChooserTask_Completed " + exception.Message);
            }
        }

        private async Task StartUploadPhotoAndDispose(string name, MemoryStream stream) {
            try {
                await StartUploadPhoto(name, stream);
            }
            finally {
                stream.Dispose();
            }
        }

        private async Task StartUploadPhoto(string name, Stream stream) {
            try {
//                Deployment.Current.Dispatcher.BeginInvoke(() => {
//                    UploadProgressBar.Visibility = Visibility.Collapsed;
//                });
                logger.info("START upload photo");
                if (!(model is DialogModelPlain)) 
                    return;

                DialogModelPlain plainModel = (DialogModelPlain) model;

                InputFile file =
                    await TelegramSession.Instance.Files.UploadFile(name, stream, delegate { });

                InputMedia media = TL.inputMediaUploadedPhoto(file);
                logger.info("END upload photo");

                Deployment.Current.Dispatcher.BeginInvoke(() => {
                    logger.info("Send media in UI thread");

                    plainModel.SendMedia(media);
                });
//                Deployment.Current.Dispatcher.BeginInvoke(() => {
//                    UploadProgressBar.Visibility = Visibility.Collapsed;
//                });
            } catch (Exception ex) {
                logger.error("exception {0}", ex);
            }
        }

        private void ToggleAttach() {
            this.Focus();
            EmojiPopup.IsOpen = false;

            AttachPopup.IsOpen = !AttachPopup.IsOpen;
            if (AttachPopup.IsOpen)
                MainPanel.Margin = new Thickness(0, 0, 0, AttachPopup.Height);
            else
                MainPanel.Margin = new Thickness(0, 0, 0, 0);  
        }

        private void Dialog_Attach(object sender, EventArgs e) {
            ToggleAttach();
        }

        private void ToggleEmoji() {
            this.Focus();

            AttachPopup.IsOpen = false;
            EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
            if (EmojiPopup.IsOpen) {
                MainPanel.Margin = new Thickness(0, 0, 0, EmojiPopup.Height);
                EmojiPanelControl.Show();
            } else
                MainPanel.Margin = new Thickness(0, 0, 0, 0);
        }

        private void Dialog_Emoji(object sender, EventArgs e) {
            ToggleEmoji();
        }

        private int GetEditorTotalHeight() {
            return (int) messageEditor.Height + (int) messageEditor.Margin.Top + (int) messageEditor.Margin.Bottom;
        }

        private void Dialog_Manage(object sender, EventArgs e) {
            messageEditor.Text += "\uD83C\uDFAA";
        }

        private void Dialog_Message_Change(object sender, TextChangedEventArgs e) {
            if (messageEditor.Text.Length > 0)
                EnableEditBox();
            else 
                DisableEditBox();
        }

        private void EnableEditBox() {
            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IsEnabled = true; 
        }

        private void DisableEditBox() {
            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IsEnabled = false; 
        }

        private void OnHeaderTap(object sender, GestureEventArgs e) {
            Peer peer = model.Peer;
            if (peer.Constructor == Constructor.peerUser) {
                NavigationService.Navigate(new Uri("/UI/Pages/UserProfile.xaml?userId=" + ((PeerUserConstructor) peer).user_id, UriKind.Relative));
            } else {
                NavigationService.Navigate(new Uri("/UI/Pages/ChatSettings.xaml?chatId=" + ((PeerChatConstructor)peer).chat_id, UriKind.Relative));

            }
        }

        private void OnOpenAttachment(object sender, GestureEventArgs e) {
            var element = (FrameworkElement)sender;
            MessageModel message = (MessageModel)element.DataContext;

            if (!(message is MessageModelDelivered))
                return;

            MessageMedia media = ((MessageModelDelivered) message).MessageMedia;
            
            MediaTransitionHelper.Instance.Media = media;

            if (media.Constructor == Constructor.messageMediaPhoto
                || media.Constructor == Constructor.messageMediaVideo) {

                NavigationService.Navigate(new Uri("/UI/Pages/MediaViewPage.xaml", UriKind.Relative));
            } else if (media.Constructor == Constructor.messageMediaGeo) {
                MediaTransitionHelper.Instance.From = message.Sender;

                NavigationService.Navigate(new Uri("/UI/Pages/MapViewPage.xaml?mode=view", UriKind.Relative));
            }

        }

        private void UserAvatarTap(object sender, GestureEventArgs e) {
            var element = (FrameworkElement)sender;
            MessageModel message = (MessageModel) element.DataContext;

            NavigationService.Navigate(new Uri("/UI/Pages/UserProfile.xaml?userId=" + message.Sender.Id, UriKind.Relative));
        }

        private void OnForwardedTap(object sender, GestureEventArgs e) {
            var element = (FrameworkElement)sender;
            MessageModel message = (MessageModel)element.DataContext;
            
            NavigationService.Navigate(new Uri("/UI/Pages/UserProfile.xaml?userId=" + message.ForwardedId, UriKind.Relative));
        }

        private void OnMessageContextMenuOpened(object sender, RoutedEventArgs e) {
            if (model is DialogModelEncrypted) {
                ContextMenu menu = sender as ContextMenu;
                menu.Items.Clear();
            }
        }

        private void OnDeleteMessage(object sender, RoutedEventArgs e) {
            var message = ((sender as MenuItem).DataContext as MessageModel);

            DoDeleteMessage(message);
        }

        private async Task DoDeleteMessage(MessageModel message) {
            List<int> idsDeleted = await TelegramSession.Instance.Api.messages_deleteMessages(new List<int>(message.Id));

            if (idsDeleted.Count > 0) {
                model.Messages.Remove(message);
            }
        }

        private void OnForwardMessage(object sender, RoutedEventArgs e) {
            var message = ((sender as MenuItem).DataContext as MessageModel);
            
            if (message.Id == 0)
                return;

            NavigationService.Navigate(new Uri("/UI/Pages/DialogListForwarding.xaml?messageId=" + message.Id, UriKind.Relative));
        }

        private void OnCopyMessage(object sender, RoutedEventArgs e) {
            var message = ((sender as MenuItem).DataContext as MessageModel);
            Clipboard.SetText(message.Text);
        }

        private async void AttachLocation(object sender, GestureEventArgs e) {
//            Geolocator geolocator = new Geolocator();
//
//            Geoposition geoposition = await geolocator.GetGeopositionAsync(
//                maximumAge: TimeSpan.FromMinutes(5),
//                timeout: TimeSpan.FromSeconds(10)
//            );
//
//            if (geoposition == null)
//                return;
//
//            if (!(model is DialogModelPlain))
//                return;
//
//            MessageBoxResult result = MessageBox.Show("Send your location to " + model.Title + "?",
//"Confirm action", MessageBoxButton.OKCancel);
//
//            if (result == MessageBoxResult.OK) {
//                InputGeoPoint point = TL.inputGeoPoint(geoposition.Coordinate.Latitude, geoposition.Coordinate.Longitude);
//                InputMedia geoMedia = TL.inputMediaGeoPoint(point);
//
//                ((DialogModelPlain) model).SendMedia(geoMedia);
//            }
            int modelId = TelegramSession.Instance.Dialogs.Model.Dialogs.IndexOf(model);
            NavigationService.Navigate(new Uri("/UI/Pages/MapPickPage.xaml?fromModelId=" + modelId, UriKind.Relative));
        }

        private void PickAndSendDoc(object sender, GestureEventArgs e) {
            var photo = new PhotoChooserTask { ShowCamera = true };
            photo.Completed += docChooserTask_Completed;
            photo.Show();
        }

        private void PickAndSendContact(object sender, GestureEventArgs e) {
            int modelId = TelegramSession.Instance.Dialogs.Model.Dialogs.IndexOf(model);
            NavigationService.Navigate(new Uri("/UI/Pages/SendContact.xaml?modelId=" + modelId, UriKind.Relative));
        }

        private void OnContactTap(object sender, GestureEventArgs e) {
            var element = (FrameworkElement)sender;
            MessageModel message = (MessageModel)element.DataContext;

            if (!(message is MessageModelDelivered))
                return;

            MessageModelDelivered messageDelivered = (MessageModelDelivered) message;
            MessageMedia media = messageDelivered.MessageMedia;
            if (media.Constructor != Constructor.messageMediaContact)
                return;

            MessageMediaContactConstructor mediaContact = (MessageMediaContactConstructor) media;

            SaveContactTask saveContactTask = new SaveContactTask();
            saveContactTask.Completed += new EventHandler<SaveContactResult>(saveContactTask_Completed);

            saveContactTask.FirstName = mediaContact.first_name;
            saveContactTask.LastName = mediaContact.last_name;
            saveContactTask.MobilePhone = "+" + mediaContact.phone_number;
            
            saveContactTask.Show();
        }

        private void saveContactTask_Completed(object sender, SaveContactResult e) {
            logger.info("User saved successfully");
        }
    }
}