using Pomodoro.Models;
using Pomodoro.Services;
using Xunit;

namespace Pomodoro.Tests
{
    public class GatewayConfigureTests
    {
        [Fact]
        public void Todoist_has_a_token_once_configured()
        {
            HttpTodoistGateway gateway = new HttpTodoistGateway();
            Assert.False(gateway.HasToken);

            gateway.Configure(new AppSettings { TodoistToken = "abc" });

            Assert.True(gateway.HasToken);
        }

        [Fact]
        public void ClickUp_needs_both_a_token_and_a_list()
        {
            HttpClickUpGateway gateway = new HttpClickUpGateway();

            gateway.Configure(new AppSettings { ClickUpToken = "k", ClickUpListId = "" });
            Assert.False(gateway.HasToken);

            gateway.Configure(new AppSettings { ClickUpToken = "k", ClickUpListId = "123" });
            Assert.True(gateway.HasToken);
        }
    }
}
