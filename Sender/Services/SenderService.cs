﻿using Microsoft.Extensions.Logging;
using PayBot.Configuration;
using Sender.DataSource.Base;
using Sender.DataSource.GoogleTabledataSource;
using Sqllite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Utils;
using Utils.Logger;

namespace Sender.Services
{
    public class SenderService : ISenderService
    {
        protected readonly IBotLogger _logger;
        protected readonly IPhoneHelper _phoneHelper;
        private readonly ILogger<SenderService> _toFileLogger;
        protected readonly IDataSource _messageDataSource;

        protected readonly IConfigService _configService;
        protected readonly UserContext _userContext;
        protected readonly StateContext _stateContext;
        protected readonly ISenderAgentProvider _senderAgentProvider;
        public SenderService
            (IConfigService configService,
            IBotLogger logger,
            IPhoneHelper phoneHelper,
            ILogger<SenderService> toFileLogger,
            IDataSource messageDataSource,
            UserContext userContext,
            StateContext stateContext,
            ISenderAgentProvider senderAgentProvider)
        {
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _stateContext = stateContext ?? throw new ArgumentNullException(nameof(stateContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toFileLogger = toFileLogger ?? throw new ArgumentNullException(nameof(toFileLogger));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _phoneHelper = phoneHelper ?? throw new ArgumentNullException(nameof(phoneHelper));
            _messageDataSource = messageDataSource ?? throw new ArgumentNullException(nameof(messageDataSource));
            _senderAgentProvider = senderAgentProvider ?? throw new ArgumentNullException(nameof(senderAgentProvider));
        }

        protected Config _config => _configService.Config;

        private bool IsListValid()
        {
            foreach (var spreedSheet in _config.Spreadsheets) {
                foreach (var list in spreedSheet.Lists)
                {
                    if (!Regex.IsMatch(list.Date, @"^[a-zA-Z]+$"))
                        return false;
                    if (!Regex.IsMatch(list.IsSendedColumn, @"^[a-zA-Z]+$"))
                        return false;
                    if (!Regex.IsMatch(list.MessageText, @"^[a-zA-Z]+$"))
                        return false;
                    if (!Regex.IsMatch(list.Status, @"^[a-zA-Z]+$"))
                        return false;
                    if (!Regex.IsMatch(list.TgUser, @"^[a-zA-Z]+$"))
                        return false;
                }
            }
            return true;
        }

        public async Task Process(CancellationToken cancellation)
        {
            var errorList = new List<string>();
            var sendedList = new List<SendedMessage>();
            var isErrorSended = false;
            var isSuccessSended = false;
            try
            {
                if (!CheckEnable())
                {
                    _toFileLogger.LogInformation("Sendidng stoped. Do nothing.");
                    _logger.LogSended($"Рассылка остановлена, ничего не отправляю", null);
                    return;
                }
                _toFileLogger.LogInformation("Start sending...");
                _logger.LogSystem($"Начинаю отправку сообщений...", null);

                if (!IsListValid())
                    throw new InvalidOperationException("Invalid config file!");

                var rows = await _messageDataSource.GetMessages(_config);
                var sendedMesageCount = 0;
                if (rows != null)
                {
                    var rowsForUpdate = new Dictionary<string, List<INeedSend>>();
                    foreach (var message in rows.OrderBy(x => x.LastModifiedDate))
                    {
                        {
                            var text = message.Text;
                            if (message.To == null)
                            {
                                var list = message.CellForUpdate.Substring(0, message.CellForUpdate.IndexOf('!')); // Regex.Match(message.CellForUpdate, @"/^(.*?)\!/").Groups[0];
                                var rownum = message.CellForUpdate.Substring(message.CellForUpdate.IndexOf('!') + 1, message.CellForUpdate.Length - list.Length - 1);// Regex.Match(message.CellForUpdate, @"/[^!]*$/").Groups[0];
                                errorList.Add
                                    ($"У пользователя в таблице {message.Table } на листе {list} в строке {rownum} не указан номер телефона, сообщение НЕ отправлено!");
                                continue;
                            }

                            if (_phoneHelper.IsPhone(message.To))
                            {
                                message.To = _phoneHelper.Format(message.To);
                            }

                            var phone = message.To;
                            var senderAgent = _senderAgentProvider.Resolve(message.SenderType);
                            var sendResult = await senderAgent.Send(message);

                            if (sendResult.IsSuccess)
                            {
                                sendedList.Add(new SendedMessage() {
                                    Message = message.Text,
                                    To = message.To
                                });
                                if (!rowsForUpdate.ContainsKey(message.Table))
                                    rowsForUpdate[message.Table] = new List<INeedSend>();
                                rowsForUpdate[message.Table].Add(message);
                                sendedMesageCount++;
                            }
                            else
                            {
                                errorList.Add($"Не удалось отправить сообщение пользователю {message.To}. Ошибка: {sendResult.Error}");
                            }
                        }
                    }

                    if (rowsForUpdate.Count > 0)
                    {
                        foreach (var item in rowsForUpdate)
                        {
                            var updateResult = await _messageDataSource.UpdateMessageStatus(item.Value);
                        }
                    }
                    _logger.LogErrorList(errorList);
                    isErrorSended = true;
                    _logger.LogSendedList(sendedList);
                    isSuccessSended = true;

                }
                _logger.LogSystem($"Отправка сообщений закончена. Сообщений отправлено: {sendedMesageCount}", null);
                _toFileLogger.LogInformation($"End sending. Message count: {sendedMesageCount}");
            }
            catch (Exception err)
            {
                _toFileLogger.LogError(err, err.Message);
                if (!isErrorSended)
                    _logger.LogErrorList(errorList);
                if (!isSuccessSended)
                    _logger.LogSendedList(sendedList);
                _logger.LogError($"Произошла непредвиденная ошибка во время отправки сообщений! Подробнее: {err.Message} . Stack Trace : {err.StackTrace}");
            }
        }

        private bool CheckEnable()
        {
            if (_stateContext.States.First().IsEnabled == -1)
                return false;

            return true;
        }
    }
}
