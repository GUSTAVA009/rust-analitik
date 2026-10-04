package com.hapych.admin

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.nio.charset.StandardCharsets
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class SecurePrefs(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences("hapych_admin", Context.MODE_PRIVATE)
    private val alias = "hapych_rcon_secret_v1"

    fun load(): AppSettings = AppSettings(
        host = prefs.getString("host", "") ?: "",
        port = prefs.getString("port", "28016") ?: "28016",
        password = decrypt(prefs.getString("password", "") ?: ""),
        useTls = prefs.getBoolean("tls", false),
        demoMode = prefs.getBoolean("demo", true),
        autoReconnect = prefs.getBoolean("reconnect", true),
        rconMode = runCatching {
            RconMode.valueOf(prefs.getString("rcon_mode", RconMode.AUTO.name) ?: RconMode.AUTO.name)
        }.getOrDefault(RconMode.AUTO)
    )

    fun save(settings: AppSettings) {
        prefs.edit()
            .putString("host", settings.host.trim())
            .putString("port", settings.port.trim())
            .putString("password", encrypt(settings.password))
            .putBoolean("tls", settings.useTls)
            .putBoolean("demo", settings.demoMode)
            .putBoolean("reconnect", settings.autoReconnect)
            .putString("rcon_mode", settings.rconMode.name)
            .apply()
    }

    private fun getKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(alias, null) as? SecretKey)?.let { return it }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(
                alias,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .build()
        )
        return generator.generateKey()
    }

    private fun encrypt(value: String): String {
        if (value.isBlank()) return ""
        return runCatching {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, getKey())
            val encrypted = cipher.doFinal(value.toByteArray(StandardCharsets.UTF_8))
            val iv = Base64.encodeToString(cipher.iv, Base64.NO_WRAP)
            val body = Base64.encodeToString(encrypted, Base64.NO_WRAP)
            "$iv:$body"
        }.getOrDefault("")
    }

    private fun decrypt(value: String): String {
        if (!value.contains(':')) return ""
        return runCatching {
            val parts = value.split(':', limit = 2)
            val iv = Base64.decode(parts[0], Base64.NO_WRAP)
            val body = Base64.decode(parts[1], Base64.NO_WRAP)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, getKey(), GCMParameterSpec(128, iv))
            String(cipher.doFinal(body), StandardCharsets.UTF_8)
        }.getOrDefault("")
    }
}
