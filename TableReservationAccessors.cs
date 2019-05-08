using System;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
namespace firstbot
{
    public class TableReservationAccessors
    {
        public ConversationState ConversationState { get; }

        public TableReservationAccessors(ConversationState conversationState)
        {
            ConversationState = conversationState;
        }

        public IStatePropertyAccessor<TableReservation> TableReservationState { get; set; }

        public IStatePropertyAccessor<DialogState> ConversationDialogState { get; set; }
    }
}
