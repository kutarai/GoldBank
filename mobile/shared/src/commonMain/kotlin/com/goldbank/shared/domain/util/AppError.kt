package com.goldbank.shared.domain.util

sealed interface AppError {
    val message: String

    data class Network(override val message: String) : AppError
    data class Unauthenticated(override val message: String) : AppError
    data class Server(val code: String, override val message: String) : AppError
    data class Validation(val field: String, override val message: String) : AppError
    data class Unknown(val throwable: Throwable) : AppError {
        override val message: String get() = throwable.message ?: "Unknown error"
    }
}
