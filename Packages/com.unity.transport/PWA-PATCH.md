# Project patch on Unity Transport 2.6.0

This is an embedded copy of the official 2.6.0 package. Preserve LICENSE.md and original copyright notices.
The upstream sources changed by the project are:

- Runtime/WebSocket/WebSocket.cs
- Runtime/Layers/WebSocketLayer.cs
- Runtime/Layers/TLSLayer.cs
- Runtime/Layers/TopLayer.cs
- Runtime/TCPNetworkInterface.cs

Native WebSocket requests originally use the resolved IP in HTTP Host even when TLS has an original
hostname. Railway routes by Host: a certificate-validated request with the domain returns 101,
while the same request with an IP Host returns 404. The patch carries the configured TLS hostname
into the client handshake and uses it for Host. Non-TLS/local connections keep the original IP behavior.
TLS SNI and trust-store validation are unchanged.

When the receive queue is full, a deferred frame keeps its original mask and continuation
length. Buffered frames are retried even when the socket supplies no new bytes.

The TLS receive layer reserves enough space for a complete plaintext record and its carry-over.
It continues parsing when a TLS control record consumes bytes without producing plaintext.
Disconnect diagnostics distinguish TLS errors, WebSocket close status, and remote TCP closes.

NetworkSandbox separately resolves DNS before starting NGO because NGO 2.4.4 enables its native
hostname resolution only for Unity 6000.1+, whereas this project uses 6000.0.60f1.

When upgrading Transport, verify both domain-based WSS through Railway and local WS before removing
this embedded package. Do not modify Library/PackageCache as a workaround.
