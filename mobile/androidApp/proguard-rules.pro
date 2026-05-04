# gRPC
-keep class io.grpc.** { *; }
-keepnames class io.grpc.** { *; }
-dontwarn io.grpc.**

# Protobuf
-keep class com.google.protobuf.** { *; }
-keepnames class com.google.protobuf.** { *; }
-dontwarn com.google.protobuf.**

# gRPC Kotlin stubs
-keep class goldbank.v1.** { *; }
-keepnames class goldbank.v1.** { *; }

# OkHttp
-dontwarn okhttp3.**
-dontwarn okio.**
-keep class okhttp3.** { *; }

# Kotlin serialization
-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.AnnotationsKt
-keepclassmembers class kotlinx.serialization.json.** { *** Companion; }
-keepclasseswithmembers class kotlinx.serialization.json.** { kotlinx.serialization.KSerializer serializer(...); }
