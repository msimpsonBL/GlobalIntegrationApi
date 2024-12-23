﻿using BL.Gateway.EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using GlobalIntegrationApi.Queries;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data.Common;

namespace GlobalIntegrationApi.IntegrationEvents.EventHandling;

public class RequestStatusChangedToCancelledIntegrationEventHandler : IIntegrationEventHandler<RequestStatusChangedToCancelledIntegrationEvent>
{
    private readonly ILogger<NewRsiMessageSubmittedIntegrationEventHandler> _logger;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly IHubContext<StatusHub, INotificationClient> _hubContext;
    private readonly IGlobalDataQueries _globalDataQueries;

    public RequestStatusChangedToCancelledIntegrationEventHandler(GlobalIntegrationContext context, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory,
                IGlobalDataQueries dataqueries, IHubContext<StatusHub, INotificationClient> hubContext, ILogger<NewRsiMessageSubmittedIntegrationEventHandler> logger)
    {
        _globalIntContext = context ?? throw new ArgumentException(nameof(context));
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentException(nameof(integrationEventLogServiceFactory));
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _globalDataQueries = dataqueries ?? throw new ArgumentNullException(nameof(dataqueries));
    }

    public async Task Handle(RequestStatusChangedToCancelledIntegrationEvent @event)
    {
        _logger.LogInformation("Global Integration event received: { @event } for Identifier: { @identifier }", @event.GetType(), @event.Identifier);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            //TODO could loop through backed up messages here if required???
            await _eventLogService.SaveEventAsync(@event, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }

        //update the client here
        var newAuditForId = await _globalDataQueries.GetAuditForIdentifier(@event.Identifier);
        await _hubContext.Clients.All.SendStatusUpdate(@event.Identifier, JsonConvert.SerializeObject(newAuditForId));
    }
}
