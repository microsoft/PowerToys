// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AppStateServiceTests
{
    private Mock<IPersistenceService> _mockPersistence = null!;
    private Mock<IApplicationInfoService> _mockAppInfo = null!;
    private string _testDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockPersistence = new Mock<IPersistenceService>();
        _mockAppInfo = new Mock<IApplicationInfoService>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CmdPalTest_{Guid.NewGuid():N}");
        _mockAppInfo.Setup(a => a.ConfigDirectory).Returns(_testDirectory);

        // Default: Load returns a new AppStateModel
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<AppStateModel>>()))
            .Returns(new AppStateModel());
    }

    [TestMethod]
    public void Constructor_LoadsState_ViaPersistenceService()
    {
        // Arrange
        var expectedState = new AppStateModel
        {
            RunHistory = ImmutableList.Create("command1", "command2"),
        };
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<AppStateModel>>()))
            .Returns(expectedState);

        // Act
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert
        Assert.IsNotNull(service.State);
        Assert.AreEqual(2, service.State.RunHistory.Count);
        Assert.AreEqual("command1", service.State.RunHistory[0]);
        _mockPersistence.Verify(
            p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<AppStateModel>>()),
            Times.Once);
    }

    [TestMethod]
    public void State_ReturnsLoadedModel()
    {
        // Arrange
        var expectedState = new AppStateModel();
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<AppStateModel>>()))
            .Returns(expectedState);

        // Act
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert
        Assert.AreSame(expectedState, service.State);
    }

    [TestMethod]
    public void Save_DelegatesToPersistenceService()
    {
        // Arrange
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);
        service.UpdateState(s => s with { RunHistory = s.RunHistory.Add("test-command") });
        _mockPersistence.Invocations.Clear(); // Reset after Arrange — UpdateState also persists

        // Act
        service.Save();

        // Assert
        _mockPersistence.Verify(
            p => p.Save(
                service.State,
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<AppStateModel>>()),
            Times.Once);
    }

    [TestMethod]
    public void Save_RaisesStateChangedEvent()
    {
        // Arrange
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);
        var eventRaised = false;
        service.StateChanged += (sender, state) =>
        {
            eventRaised = true;
        };

        // Act
        service.Save();

        // Assert
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void StateChanged_PassesCorrectArguments()
    {
        // Arrange
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);
        IAppStateService? receivedSender = null;
        AppStateModel? receivedState = null;

        service.StateChanged += (sender, state) =>
        {
            receivedSender = sender;
            receivedState = state;
        };

        // Act
        service.Save();

        // Assert
        Assert.AreSame(service, receivedSender);
        Assert.AreSame(service.State, receivedState);
    }

    [TestMethod]
    public void Save_Always_RaisesStateChangedEvent()
    {
        // Arrange - AppStateService.Save() should always raise StateChanged
        // (unlike SettingsService which has hotReload parameter)
        var service = new AppStateService(_mockPersistence.Object, _mockAppInfo.Object);
        var eventCount = 0;

        service.StateChanged += (sender, state) =>
        {
            eventCount++;
        };

        // Act
        service.Save();
        service.Save();

        // Assert
        Assert.AreEqual(2, eventCount);
    }
}
