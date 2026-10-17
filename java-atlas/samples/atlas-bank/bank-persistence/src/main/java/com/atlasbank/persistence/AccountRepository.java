package com.atlasbank.persistence;

import com.atlasbank.domain.model.Account;
import java.util.Optional;

public interface AccountRepository {

    Optional<Account> findById(String id);

    void save(Account account);
}
