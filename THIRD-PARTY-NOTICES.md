# Third-party notices

upsy is MIT-licensed. Parts of it are based on the work below.

## rabits/ha-ef-ble

- Project: https://github.com/rabits/ha-ef-ble (Home Assistant integration for EcoFlow devices over Bluetooth LE)
- License: Apache License 2.0, https://www.apache.org/licenses/LICENSE-2.0
- Copyright: the ha-ef-ble contributors

upsy's EcoFlow support re-implements, in C#, protocol knowledge from that project:
- the v2 packet and 5A5A frame layouts with their CRCs;
- the encryption type 7 handshake (secp160r1 ECDH, session key from the key table, authentication with the account token);
- the account login request;
- the DELTA 2 heartbeat field offsets.

No source code was copied. The files that carry this knowledge say so in their header and were written for upsy:
- `src/Upsy.Core/Protocol/EcoFlow/EcoFlowPacket.cs`
- `src/Upsy.Core/Protocol/EcoFlow/EcoFlowCrypto.cs`
- `src/Upsy.Core/Protocol/EcoFlow/EcoFlowSession.cs`
- `src/Upsy.Core/Protocol/EcoFlow/Delta2Decoder.cs`
- `src/Upsy.Core/Protocol/EcoFlow/EcoFlowAccount.cs`

The heartbeat offsets were also checked against captures from a real DELTA 2.

### EcoFlow key table

The session key derivation uses a constant table that comes from EcoFlow's app and is published in ha-ef-ble as `eflib/keydata.py`. It belongs to EcoFlow and is not part of this repository or its license. `tools/get-ecoflow-keydata.ps1` downloads it from a pinned ha-ef-ble commit, verifies its SHA-256, and stores it on the user's PC in `%LOCALAPPDATA%\upsy\ecoflow-keydata.bin`.

## nielsole/ecoflow-bt-reverse-engineering

- Project: https://github.com/nielsole/ecoflow-bt-reverse-engineering
- License: MIT

One packet published there is used as a CRC test vector in `tests/Upsy.Core.Tests/EcoFlowTests.cs`.

## Trademarks

upsy is not affiliated with Bluetti or EcoFlow. "Bluetti", "EB3A", "EcoFlow" and "DELTA 2" are used only to name the devices this tool talks to.
