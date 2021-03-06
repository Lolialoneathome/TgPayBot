﻿using PayBot.Configuration;
using Sender.DataSource.Base;
using Sqllite;
using System;
using System.Linq;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Utils;

namespace Sender.DataSource.SenderAgents
{
    public class TwilioSmsSenderAgent : ISenderAgent
    {
        protected readonly IConfigService _configService;
        protected readonly SqlliteDbContext _dbContext;
        protected readonly IPhoneHelper _phoneHelper;
        public TwilioSmsSenderAgent(IConfigService configService, SqlliteDbContext dbContext, IPhoneHelper phoneHelper)
        {
            _configService = configService ?? throw new ArgumentNullException();
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _phoneHelper = phoneHelper;
        }

        public async Task<MessageSendResult> Send(INeedSend message)
        {
            var accountSid = _configService.Config.Twillo.Sid;
            var authToken = _configService.Config.Twillo.Token;

            TwilioClient.Init(accountSid, authToken);

            await MessageResource.CreateAsync(
                to: new PhoneNumber(_phoneHelper.GetPhoneNumberForTwilio(message.To)),
                from: new PhoneNumber(_configService.Config.Twillo.PhoneNumber),
                body: message.Text);

            return new MessageSendResult()
            {
                Message = message,
                IsSuccess = true
            };
        }
    }
}
