add_executable(runewire_inject_tests
    rw_inject_tests.cpp
)

target_link_libraries(runewire_inject_tests PRIVATE Runewire.Injector)

add_test(NAME runewire_inject_tests COMMAND runewire_inject_tests)
set_tests_properties(runewire_inject_tests PROPERTIES
    ENVIRONMENT_MODIFICATION "PATH=path_list_prepend:$<TARGET_FILE_DIR:Runewire.Injector>"
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}"
)
