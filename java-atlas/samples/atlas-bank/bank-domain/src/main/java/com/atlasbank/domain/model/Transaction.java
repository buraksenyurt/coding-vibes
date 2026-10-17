package com.atlasbank.domain.model;

import com.atlasbank.common.Money;
import java.time.Instant;

public record Transaction(String accountId, Money amount, Instant at) {
}
