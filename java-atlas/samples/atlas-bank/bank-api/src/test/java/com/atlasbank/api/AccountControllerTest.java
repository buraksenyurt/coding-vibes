package com.atlasbank.api;

import static org.junit.jupiter.api.Assertions.assertEquals;

import org.junit.jupiter.api.Test;

class AccountControllerTest {

    @Test
    void unknownAccountIsAnEmptyObject() throws Exception {
        assertEquals("{}", new AccountController().show("missing"));
    }
}
