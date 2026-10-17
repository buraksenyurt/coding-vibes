plugins {
    `java-library`
}

dependencies {
    // Product exposes Sku in its public API, so shop-core is an `api` dependency.
    api(project(":shop-core"))
    implementation(libs.jackson.databind)
    // implementation("com.example:ghost:1.0")   <- commented out: must be ignored
    /* testImplementation("com.example:ghost-test:1.0") */
}
