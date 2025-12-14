add_executable(runewire_inject_tests
    rw_inject_tests.cpp
)

target_link_libraries(runewire_inject_tests PRIVATE Runewire.Injector)

add_test(NAME runewire_inject_tests COMMAND runewire_inject_tests)
