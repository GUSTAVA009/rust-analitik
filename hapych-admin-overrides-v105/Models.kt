package com.hapych.admin

data class ServerStats(
    val hostname: String = "HAPYCH RUST",
    val players: Int = 0,
    val maxPlayers: Int = 100,
    val queued: Int = 0,
    val joining: Int = 0,
    val fps: Float = 0f,
    val memoryMb: Long = 0L,
    val entities: Int = 0,
    val uptimeSeconds: Long = 0L,
    val map: String = "Procedural Map",
    val gameTime: String = "--:--"
)

data class RustPlayer(
    val steamId: String,
    val name: String,
    val ping: Int = 0,
    val address: String = "",
    val connectedSeconds: Long = 0L
)

data class ConsoleLine(
    val time: String,
    val text: String,
    val kind: LineKind = LineKind.INFO
)

enum class LineKind { INFO, SUCCESS, WARNING, ERROR, COMMAND }

enum class RconMode { AUTO, WEB, LEGACY }

data class AppSettings(
    val host: String = "",
    val port: String = "28016",
    val password: String = "",
    val useTls: Boolean = false,
    val demoMode: Boolean = true,
    val autoReconnect: Boolean = true,
    val rconMode: RconMode = RconMode.AUTO
)

enum class ConnectionState { DEMO, DISCONNECTED, CONNECTING, CONNECTED, ERROR }
