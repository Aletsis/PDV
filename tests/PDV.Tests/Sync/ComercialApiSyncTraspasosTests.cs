using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Infrastructure.Common;
using Xunit;

namespace PDV.Tests.Sync;

public class ComercialApiSyncTraspasosTests
{
    [Fact]
    public async Task SendTraspasoToComercialAsync_SendsToSalidasAlmacenEndpoint_WithCorrectPayload()
    {
        // Arrange
        var mockRepo = new Mock<ISystemConfigurationRepository>();
        var config = new SystemConfiguration("Empresa Test", "AAA010101AAA", "601");
        config.UpdateComercialApiSettings("http://localhost:5000", "test-key");
        mockRepo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var mockLogger = new Mock<ILogger<ComercialApiSyncService>>();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new CreateDocumentoResultDto
                {
                    IdDocumento = 999,
                    CodigoConcepto = "TRAS-SUC2",
                    Serie = "TR",
                    Folio = "101"
                }))
            });

        var service = new TestableComercialApiSyncService(mockRepo.Object, mockLogger.Object, handlerMock.Object);

        var command = new SendTraspasoDto
        {
            CodigoConcepto = "TRAS-SUC2",
            Serie = "TR",
            Folio = 101,
            CodigoAlmacenOrigen = "ALM-MATRIZ",
            CodigoAlmacenDestino = "ALM-SUC2",
            Referencia = "Traspaso Semanal",
            Observaciones = "Envío prioritario",
            Usuario = "cajero1",
            Partidas = new List<TraspasoPartidaSyncDto>
            {
                new() { CodigoProducto = "PROD-01", Unidades = 10 },
                new() { CodigoProducto = "PROD-02", Unidades = 5 }
            }
        };

        // Act
        var result = await service.SendTraspasoToComercialAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(999, result!.IdDocumento);
        Assert.Equal("TRAS-SUC2", result.CodigoConcepto);
        Assert.Equal("TR", result.Serie);
        Assert.Equal("101", result.Folio);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("http://localhost:5000/api/SalidasAlmacen", capturedRequest.RequestUri?.ToString());
        Assert.True(capturedRequest.Headers.Contains("X-Usuario"));
        Assert.Equal("cajero1", capturedRequest.Headers.GetValues("X-Usuario").First());

        Assert.NotNull(capturedBody);
        var jsonDoc = JsonDocument.Parse(capturedBody!);
        var root = jsonDoc.RootElement;

        Assert.Equal("TRAS-SUC2", root.GetProperty("codigoConcepto").GetString());
        Assert.Equal("TR", root.GetProperty("serie").GetString());
        Assert.Equal("Traspaso Semanal", root.GetProperty("referencia").GetString());
        Assert.Contains("ALM-SUC2", root.GetProperty("observaciones").GetString());

        var partidas = root.GetProperty("partidas");
        Assert.Equal(2, partidas.GetArrayLength());

        var partida1 = partidas[0];
        Assert.Equal("PROD-01", partida1.GetProperty("codigoProducto").GetString());
        Assert.Equal("ALM-MATRIZ", partida1.GetProperty("codigoAlmacen").GetString());
        Assert.Equal(10, partida1.GetProperty("unidades").GetDouble());

        var partida2 = partidas[1];
        Assert.Equal("PROD-02", partida2.GetProperty("codigoProducto").GetString());
        Assert.Equal("ALM-MATRIZ", partida2.GetProperty("codigoAlmacen").GetString());
        Assert.Equal(5, partida2.GetProperty("unidades").GetDouble());
    }

    private class TestableComercialApiSyncService : ComercialApiSyncService
    {
        private readonly HttpMessageHandler _handler;

        public TestableComercialApiSyncService(
            ISystemConfigurationRepository systemConfigRepository,
            ILogger<ComercialApiSyncService> logger,
            HttpMessageHandler handler) : base(systemConfigRepository, logger)
        {
            _handler = handler;
        }

        protected override Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken, string? usuario = null)
        {
            var client = new HttpClient(_handler)
            {
                BaseAddress = new Uri("http://localhost:5000/")
            };
            if (!string.IsNullOrWhiteSpace(usuario))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Usuario", usuario);
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Name", usuario);
            }
            return Task.FromResult(client);
        }
    }
}
