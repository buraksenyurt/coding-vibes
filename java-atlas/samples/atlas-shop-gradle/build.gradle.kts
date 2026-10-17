// The root project has no sources; it only configures the subprojects.
subprojects {
    group = "com.atlasshop"
    version = "0.3.0"

    // Dynamic configuration like this is invisible to java-atlas (by design).
    apply(plugin = "java-library")
}
