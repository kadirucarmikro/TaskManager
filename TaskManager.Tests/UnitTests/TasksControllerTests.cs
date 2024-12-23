using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskManager.Api.Controllers;
using TaskManager.Api.Models;
using Xunit;

namespace TaskManager.Tests.UnitTests;
public class TasksControllerTests
{
    [Fact]
    public async Task GetTasks_ReturnsAllTasks()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TaskContext>()
            .UseInMemoryDatabase(databaseName: "GetTasks_ReturnsAllTasks")
            .Options;

        using (var context = new TaskContext(options))
        {
            context.Tasks.Add(new Task { Id = 1, Title = "Test Task 1", Description = "Description 1" });
            context.Tasks.Add(new Task { Id = 2, Title = "Test Task 2", Description = "Description 2" });
            context.SaveChanges();
        }

        using (var context = new TaskContext(options))
        {
            var controller = new TasksController(context);

            // Act
            var result = await controller.GetTasks();

            // Assert
            var okResult = Assert.IsType<ActionResult<IEnumerable<Task>>>(result);
            var tasks = Assert.IsAssignableFrom<IEnumerable<Task>>(okResult.Value);
            Assert.Equal(2, tasks.Count());
        }
    }
}