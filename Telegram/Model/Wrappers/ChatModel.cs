﻿using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Telegram.Annotations;
using Telegram.MTProto;

namespace Telegram.Model.Wrappers {
    public delegate void ChatModelChangeHandler();
    public class ChatModel : INotifyPropertyChanged {
        private Chat chat;
        public event ChatModelChangeHandler ChangeEvent;
        public ChatModel(Chat chat) {
            this.chat = chat;
        }

        public void SetChat(Chat chat) {
            this.chat = chat;
            ChangeEvent();
            OnPropertyChanged("Title");
            OnPropertyChanged("Status");
        }

        public int Id {
            get {
                switch (chat.Constructor) {
                    case Constructor.chatEmpty:
                        return ((ChatEmptyConstructor) chat).id;
                    case Constructor.chat:
                        return ((ChatConstructor) chat).id;
                    case Constructor.chatForbidden:
                        return ((ChatForbiddenConstructor) chat).id;
                    default:
                        throw new InvalidDataException("invalid constructor");
                }
            }
        }

        public Chat RawChat {
            get {
                return chat;
            }
        }

        public string Title {
            get {
                switch (chat.Constructor) {
                    case Constructor.chatEmpty:
                        return "empty";
                    case Constructor.chat:
                        return ((ChatConstructor)chat).title;
                    case Constructor.chatForbidden:
                        return ((ChatForbiddenConstructor)chat).title;
                    default:
                        throw new InvalidDataException("invalid constructor");
                }
            }
        }

        public string Status {
            get {
                switch (chat.Constructor) {
                    case Constructor.chatEmpty:
                        return "chat is empty: 0 users";
                    case Constructor.chat:
                        return ((ChatConstructor)chat).participants_count + " users";
                    case Constructor.chatForbidden:
                        return "blocked";
                    default:
                        throw new InvalidDataException("invalid constructor");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
