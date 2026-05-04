package com.goldbank.shared.data.remote

import com.goldbank.shared.domain.util.AppError
import io.grpc.Status
import io.grpc.StatusException
import io.grpc.StatusRuntimeException

object GrpcErrorMapper {

    fun map(exception: StatusException): AppError = mapStatus(exception.status)

    fun map(exception: StatusRuntimeException): AppError = mapStatus(exception.status)

    private fun mapStatus(status: Status): AppError = when (status.code) {
        Status.Code.UNAUTHENTICATED -> AppError.Unauthenticated(
            status.description ?: "Authentication required"
        )
        Status.Code.PERMISSION_DENIED -> AppError.Unauthenticated(
            status.description ?: "Permission denied"
        )
        Status.Code.INVALID_ARGUMENT -> AppError.Validation(
            field = "",
            message = status.description ?: "Invalid input"
        )
        Status.Code.NOT_FOUND -> AppError.Server(
            code = "NOT_FOUND",
            message = status.description ?: "Resource not found"
        )
        Status.Code.ALREADY_EXISTS -> AppError.Server(
            code = "ALREADY_EXISTS",
            message = status.description ?: "Resource already exists"
        )
        Status.Code.UNAVAILABLE -> AppError.Network(
            status.description ?: "Service unavailable"
        )
        Status.Code.DEADLINE_EXCEEDED -> AppError.Server(
            code = "DEADLINE_EXCEEDED",
            message = status.description ?: "Request timed out"
        )
        else -> AppError.Server(
            code = status.code.name,
            message = status.description ?: "Server error"
        )
    }
}
