using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Ocelot.Logging;
using Ocelot.Raft;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;

namespace Ocelot.Raft
{
    [ExcludeFromCoverage]
    [Authorize]
    [Route("raft")]
    public class RaftController : Controller
    {
        private readonly INode _node;
        private IOcelotLogger _logger;
        private string _baseSchemeUrlAndPort;
        private JsonSerializerSettings _jsonSerialiserSettings;

        public RaftController(INode node, IOcelotLoggerFactory loggerFactory, IWebHostBuilder builder)
        {
            _jsonSerialiserSettings = new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.All
            };
            _baseSchemeUrlAndPort = builder.GetSetting(WebHostDefaults.ServerUrlsKey);
            _logger = loggerFactory.CreateLogger<RaftController>();
            _node = node;
        }

        [Route("appendentries")]
        public async Task<IActionResult> AppendEntries()
        {
            using(var reader = new StreamReader(HttpContext.Request.Body))
            {
                var json = await reader.ReadToEndAsync();
                var appendEntries = JsonConvert.DeserializeObject<AppendEntries>(json, _jsonSerialiserSettings);
                _logger.LogDebug($"{_baseSchemeUrlAndPort}/appendentries called, my state is {_node.State.GetType().FullName}");
                var appendEntriesResponse = _node.Handle(appendEntries);
                return new OkObjectResult(appendEntriesResponse);
            }
        }

        [Route("requestvote")]
        public async Task<IActionResult> RequestVote()
        { 
            using(var reader = new StreamReader(HttpContext.Request.Body))
            {
                var json = await reader.ReadToEndAsync();
                var requestVote = JsonConvert.DeserializeObject<RequestVote>(json, _jsonSerialiserSettings);
                _logger.LogDebug($"{_baseSchemeUrlAndPort}/requestvote called, my state is {_node.State.GetType().FullName}");
                var requestVoteResponse = _node.Handle(requestVote);
                return new OkObjectResult(requestVoteResponse);
            }
        }

        [Route("command")]
        public async Task<IActionResult> Command()
        { 
            try
            {
                using(var reader = new StreamReader(HttpContext.Request.Body))
                {
                    var json = await reader.ReadToEndAsync();
                    var command = JsonConvert.DeserializeObject<ICommand>(json, _jsonSerialiserSettings);
                    _logger.LogDebug($"{_baseSchemeUrlAndPort}/command called, my state is {_node.State.GetType().FullName}");
                    var commandResponse = _node.Accept(command);
                    json = JsonConvert.SerializeObject(commandResponse, _jsonSerialiserSettings);
                    return StatusCode(200, json);
                }
            }
            catch(Exception e)
            {
                _logger.LogError($"THERE WAS A PROBLEM ON NODE {_node.State.CurrentState.Id}", e);
                throw e;
            }
        }
    }
}