﻿using MlkPwgen;
using MongoWebApiStarter;
using MongoWebApiStarter.Auth;
using ServiceStack;
using System.Threading.Tasks;

namespace Account.Save
{
    [Authenticate(ApplyTo.Patch)]
    public class Service : Service<Request, Response>
    {
        private bool needsEmailVerification;

        public Task<Response> PostAsync(Request r) => PatchAsync(r);

        [Need(Claim.AccountID)]
        public async Task<Response> PatchAsync(Request r)
        {
            r.ID = User.ClaimValue(Claim.AccountID); //post tampering protection

            await CheckIfEmailValidationIsNeededAsync(r);

            var acc = r.ToEntity();

            await Data.CreateOrUpdateAsync(acc);

            await SendVerificationEmailAsync(acc);

            Response.EmailSent = needsEmailVerification;
            Response.ID = acc.ID;

            return Response;
        }

        private async Task SendVerificationEmailAsync(Dom.Account a)
        {
            if (needsEmailVerification)
            {
                var code = PasswordGenerator.Generate(20);
                await Data.SetEmailValidationCodeAsync(code, a.ID);

                await new Notification
                {
                    Type = NotificationType.Account_Welcome,
                    Email = a.Email,
                    EmailSubject = "Please validate your account...",
                    ReceiverName = $"{a.FirstName} {a.LastName}",
                    SendEmail = true,
                    Mobile = a.Mobile,
                    SendSMS = a.Mobile.HasValue()
                }
                .Merge("{Salutation}", $"{a.FirstName}")
                .Merge("{ValidationLink}", $"{BaseURL}#/account/{a.ID}-{code}/validate")
                .AddToSendingQueueAsync();
            }
        }

        private async Task CheckIfEmailValidationIsNeededAsync(Request r)
        {
            if (r.ID.HasNoValue())
            {
                needsEmailVerification = true;
            }
            else if (r.ID != await Data.GetAccountIDAsync(r.EmailAddress))
            {
                needsEmailVerification = true;
            }
            else
            {
                needsEmailVerification = false;
            }
        }
    }
}
