package com.atlasbank.service.account;

import static org.junit.jupiter.api.Assertions.assertEquals;

import com.atlasbank.common.Money;
import com.atlasbank.domain.model.Account;
import com.atlasbank.domain.model.Customer;
import java.math.BigDecimal;
import java.util.Currency;
import org.junit.jupiter.api.Test;

class AccountSummaryTest {

    @Test
    void copiesTheBalance() {
        Money ten = new Money(BigDecimal.TEN, Currency.getInstance("EUR"));
        Account account = new Account("a-1", new Customer("c-1", "Ada"), ten);
        assertEquals(ten, AccountSummary.of(account).balance());
    }
}
