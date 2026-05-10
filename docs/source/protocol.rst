Network Protocol
================

This page documents the network protocol.

Core specification
------------------

* Messages are framed so that each payload can be read independently from the byte stream.
* All integer fields are little-endian.
* Floating-point values are little-endian IEEE 754 values.
* Compressed fields use the deflate format.

Frame structure
---------------

TODO

.. list-table:: Frame header
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Message type
     - 4
     - A little-endian 32-bit integer identifying the payload type.
   * - Payload length
     - 4
     - A little-endian 32-bit integer giving the payload size in bytes.
   * - Payload
     - Variable
     - The payload for the selected message type.

Initial state transfer
----------------------

When a player connects, the host may send a cached initial state so that the
player can immediately reconstruct the current map view. When present, those
messages are sent in this order:

1. Session info
2. Fog appearance
3. Map image
4. Viewport
5. Cursor

Message type IDs
----------------

.. list-table:: Message types
   :header-rows: 1

   * - ID
     - Name
     - Purpose
   * - 1
     - Session info
     - Session metadata and the initial full fog mask.
   * - 2
     - Map image
     - Encoded map image bytes.
   * - 3
     - Fog delta
     - A rectangular fog update.
   * - 4
     - Fog full
     - A full fog mask replacement.
   * - 5
     - Fog appearance
     - Fog rendering mode, colour, and texture seed.
   * - 6
     - Viewport
     - Camera position and zoom.
   * - 7
     - Cursor
     - Cursor position, appearance, and visibility.

Session info payload
--------------------

This payload is used during the initial handshake. It combines session metadata
with a full fog mask.

.. list-table:: Session info payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Session ID
     - 16
     - A 128-bit UUID. The first 4-byte field, the next 2-byte field, and the next 2-byte field are little-endian; the final 8 bytes are stored as-is.
   * - Map width
     - 4
     - The width of the map in pixels.
   * - Map height
     - 4
     - The height of the map in pixels.
   * - Fog mask
     - Variable
     - A deflate-compressed fog mask containing ``map width * map height`` bytes.

Fog mask bytes are stored in row-major order. Each byte is a reveal value:

- ``0`` means fully fogged.
- ``255`` means fully revealed.

Map image payload
-----------------

This payload contains the map image bytes exactly as transmitted by the host.
The protocol treats the image data as opaque encoded bytes and does not define
an image container format.

Fog delta payload
-----------------

This payload updates a rectangular region of the fog mask. It contains the
authoritative byte values for the addressed region.

.. list-table:: Fog delta payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - X
     - 4
     - The left edge of the region in map pixels.
   * - Y
     - 4
     - The top edge of the region in map pixels.
   * - Width
     - 4
     - The width of the region in pixels.
   * - Height
     - 4
     - The height of the region in pixels.
   * - Region data
     - Variable
     - A deflate-compressed block containing ``width * height`` bytes.

Region data is stored in row-major order. Each byte is the fog value for one
pixel in the rectangle.

Fog full payload
----------------

This payload replaces the entire fog mask in one message.

.. list-table:: Fog full payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Width
     - 4
     - The width of the fog mask in pixels.
   * - Height
     - 4
     - The height of the fog mask in pixels.
   * - Mask data
     - Variable
     - A deflate-compressed block containing ``width * height`` bytes.

Mask data is stored in row-major order.

Fog appearance payload
----------------------

This payload controls how players render the fog overlay.

.. list-table:: Fog appearance payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Fog type
     - 1
     - The ID of the fog rendering type.
   * - Red channel
     - 1
     - The red channel of the fog colour. Only applicable for the flat colour fog type.
   * - Green channel
     - 1
     - The green channel of the fog colour. Only applicable for the flat colour fog type.
   * - Blue channel
     - 1
     - The blue channel of the fog colour. Only applicable for the flat colour fog type.
   * - Texture seed
     - 16
     - A seed for deterministic fog textures. Not applicable for the flat colour fog type.

Viewport payload
----------------

This payload defines the camera state that players should apply to the map.

.. list-table:: Viewport payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Centre X
     - 8 (float64)
     - The map-space X coordinate that should be centred in the viewport.
   * - Centre Y
     - 8 (float64)
     - The map-space Y coordinate that should be centred in the viewport.
   * - Zoom level
     - 8 (float64)
     - The zoom multiplier to apply around the centered map coordinate.

Cursor payload
--------------

This payload controls the cursor shown to players.

.. list-table:: Cursor payload fields
   :header-rows: 1

   * - Field
     - Size (bytes)
     - Notes
   * - Map X
     - 8 (float64)
     - The map-space X coordinate of the cursor anchor.
   * - Map Y
     - 8 (float64)
     - The map-space Y coordinate of the cursor anchor.
   * - Cursor type
     - 4
     - The ID of the cursor type.
   * - Cursor size
     - 4
     - The rendered cursor size in screen pixels.
   * - Visible
     - 1
     - 1 if the cursor should be visible to players, 0 otherwise.
