package com.unibank.shared.data.remote

import io.grpc.ClientInterceptor
import io.grpc.ManagedChannel
import io.grpc.okhttp.OkHttpChannelBuilder
import java.util.concurrent.TimeUnit

class GrpcChannelFactory(
    private val host: String,
    private val port: Int,
    private val useTls: Boolean,
    private val interceptors: List<ClientInterceptor> = emptyList(),
) {
    fun create(): ManagedChannel {
        val builder = OkHttpChannelBuilder.forAddress(host, port)
        if (!useTls) builder.usePlaintext()
        builder.keepAliveTime(30, TimeUnit.SECONDS)
        builder.keepAliveTimeout(10, TimeUnit.SECONDS)
        builder.idleTimeout(5, TimeUnit.MINUTES)
        interceptors.forEach { builder.intercept(it) }
        return builder.build()
    }
}
