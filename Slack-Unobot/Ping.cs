using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Net;
using System.Net.Http;

namespace SlackUnobot
{
	public static class Ping
	{
		[FunctionName("Ping")]
		public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
		{
			return req.CreateResponse(HttpStatusCode.OK, "Pong");
		}
	}
}