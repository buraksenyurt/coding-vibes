package com.atlasbank.domain.model;

import com.atlasbank.common.Money;

public final class Account {

    /** Nested type: imported elsewhere as com.atlasbank.domain.model.Account.Status. */
    public enum Status { OPEN, FROZEN, CLOSED }

    private final String id;
    private final Customer owner;
    private Money balance;
    private Status status = Status.OPEN;

    public Account(String id, Customer owner, Money opening) {
        this.id = id;
        this.owner = owner;
        this.balance = opening;
    }

    public String id() { return id; }
    public Customer owner() { return owner; }
    public Money balance() { return balance; }
    public Status status() { return status; }

    public void apply(Transaction tx) {
        balance = balance.plus(tx.amount());
    }

    public void freeze() {
        status = Status.FROZEN;
    }
}
