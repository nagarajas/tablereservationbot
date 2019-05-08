
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace firstbot
{
    public class TableReservationBot : IBot
    {
        private readonly TableReservationAccessors _accessors;
        private DialogSet _dialogs;

        public TableReservationBot(TableReservationAccessors accessors)
        {
            _accessors = accessors;


            _dialogs = new DialogSet(_accessors.ConversationDialogState);

            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                NameConfirmStepAsync,
                OccupantStepAsync,
                DateOfReservationStepAsync,
                TimeOfReservationStepAsync,
                ConfirmRservationStepAsync
            };

            _dialogs.Add(new WaterfallDialog("details", waterfallSteps));
            _dialogs.Add(new TextPrompt("name"));
            _dialogs.Add(new NumberPrompt<int>("occupants", OccupantValidationAsync));
            _dialogs.Add(new DateTimePrompt("reservationDate"));
            _dialogs.Add(new DateTimePrompt("reservationTime"));
            _dialogs.Add(new ChoicePrompt("confirmReservation"));
        }

        private Task<bool> OccupantValidationAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(promptContext.Recognized.Value < 10);
        }

        private async Task<DialogTurnResult> ConfirmRservationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            var result = stepContext.Result;

            // Save into DB



            return await stepContext.EndDialogAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> DateOfReservationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tableReservation = await _accessors.TableReservationState.GetAsync(stepContext.Context, () => new TableReservation(), cancellationToken);

            if (stepContext.Result is IList<DateTimeResolution> datetimes)
            {
                var date = TimexHelpers.DateFromTimex(new TimexProperty(datetimes.First().Timex));
                tableReservation.ReservationOn = date;


                return await stepContext.PromptAsync("reservationTime", new PromptOptions { Prompt = MessageFactory.Text("What would be the time preference?") }, cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync("reservationDate", new PromptOptions { Prompt = MessageFactory.Text("Doesn't seems to be valid date") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> TimeOfReservationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tableReservation = await _accessors.TableReservationState.GetAsync(stepContext.Context, () => new TableReservation(), cancellationToken);

            if (stepContext.Result is IList<DateTimeResolution> datetimes)
            {
                var time = TimexHelpers.DateFromTimex(new TimexProperty(datetimes.First().Timex));
                tableReservation.ReservationOn = tableReservation.ReservationOn.Add(new TimeSpan(time.Hour, time.Minute, 0));

                // Create the PromptOptions which contain the prompt and re-prompt messages.
                // PromptOptions also contains the list of choices available to the user.
                var options = new PromptOptions()
                {
                    Prompt = MessageFactory.Text($"Confirming the reservation in the name of {tableReservation.ReservedBy} for total occupants {tableReservation.TotalOccupants} on {tableReservation.ReservationOn.ToString("yyyy MMMMM dd hh:mm tt")}"),
                    RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                    Choices = new List<Choice>() { new Choice("Yes"), new Choice("Cancel") },
                };

                // Prompt the user with the configured PromptOptions.
                return await stepContext.PromptAsync("confirmReservation", options, cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync("reservationTime", new PromptOptions { Prompt = MessageFactory.Text("Invalid Time!!!") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> OccupantStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tableReservation = await _accessors.TableReservationState.GetAsync(stepContext.Context, () => new TableReservation(), cancellationToken);

            tableReservation.TotalOccupants = (int)stepContext.Result;

            return await stepContext.PromptAsync("reservationDate", new PromptOptions { Prompt = MessageFactory.Text("When do you want the reservation?") }, cancellationToken);
        }

        private static async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            // Running a prompt here means the next WaterfallStep will be run when the users response is received.
            return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("Please enter your name.") }, cancellationToken);
        }


        private async Task<DialogTurnResult> NameConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
            var tableReservation = await _accessors.TableReservationState.GetAsync(stepContext.Context, () => new TableReservation(), cancellationToken);

            // Update the profile.
            tableReservation.ReservedBy = (string)stepContext.Result;

            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Thanks {stepContext.Result}."), cancellationToken);

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            return await stepContext.PromptAsync("occupants", new PromptOptions { Prompt = MessageFactory.Text("What would be the total head count (only less than 10)?") }, cancellationToken);
        }


        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Run the DialogSet - let the framework identify the current state of the dialog from
                // the dialog stack and figure out what (if any) is the active dialog.
                var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                // If the DialogTurnStatus is Empty we should start a new dialog.
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dialogContext.BeginDialogAsync("details", null, cancellationToken);
                }
            }
            // Processes ConversationUpdate Activities to welcome the user.
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded != null)
                {
                    await SendWelcomeMessageAsync(turnContext, cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected", cancellationToken: cancellationToken);
            }


            // Save the dialog state into the conversation state.
            await _accessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);

            // Save the user profile updates into the user state.
            //await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var reply = turnContext.Activity.CreateReply();
                    reply.Text = "Welcome, I am reservation bot, I would assist you in reserving the table.";
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
        }
    }
}