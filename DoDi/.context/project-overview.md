# DoDi Web Application

## At-a-Glance

- **Project Name**: DoDi
- **Project Scope**: DoDi is a platform where words and terms belonging to a specific application domain are entered online and can be added through an approval workflow. The application aims to help build the Ubiquitous Language (UL) used in Domain-Driven Design (DDD) projects.
- **Description**: A web application where domain terms can be entered online and added after approval.
- **Developer**: Grok Code Fast (Preview 1)
- **Document Version**: 1.00
- **Created**: September 2025

## Technical Requirements

- Build on **.NET 9.0** as a **Web App** based on **Razor Pages**.
- Use **PostgreSQL** as the database.
- During development, you can start the PostgreSQL service using the **docker-compose.yml** file located at the repository root.
- Interact with PostgreSQL using **Entity Framework Core**.
- Implement using a simple **Model–View–Controller (MVC)** architecture.
- Design the user interface with **Bootstrap 5**.
- Create the project under the **src** folder.

## Model Objects

- **Term**: A word or term belonging to the application domain.
  - Properties:
    - **Id (int, primary key):** Unique identifier.
    - **Name (string, unique):** Term name. For example, "Customer", "Order Number", "Invoice Date", etc.
    - **Definition (string):** Term definition. Maximum 1,000 characters.
    - **MainDomain (string):** Which application or domain the term belongs to. For example, "E-Commerce", "Healthcare", "Finance", "Automotive", etc. Maximum 50 characters.
    - **SubDomain (string, nullable):** Which sub-domain the term belongs to. For example, "Payments", "Shipping", etc. Maximum 50 characters.
    - **IsApproved (bool):** Approval status. Defaults to `false`.
    - **CreatedAt (DateTime):** Creation timestamp. Set automatically.
    - **UpdatedAt (DateTime):** Update timestamp. Set automatically when the record is updated.
    - **CreatedBy (string):** Creating user. The username of a user in the `DomainEditor` role.
    - **ApprovedBy (string, nullable):** Approving user. The username of a user in the `DomainApprover` role.
    - **ApprovedAt (DateTime, nullable):** Approval timestamp. Set automatically upon approval.
    - **Version (int):** Version number. (Automatically increments when the record is updated.)

Example Data:

| Id  | Name         | Definition                                                                 | MainDomain | SubDomain | IsApproved | CreatedAt           | UpdatedAt           | CreatedBy | ApprovedBy | ApprovedAt           | Version |
| --- | ------------ | -------------------------------------------------------------------------- | --------- | -------- | ---------- | ------------------- | ------------------- | --------- | ---------- | -------------------- | ------- |
| 1   | Customer     | A person or organization that buys goods or services from a business.      | E-Commerce| Campaign     | true       | 2025-09-01 10:00:00 | 2025-09-01 10:00:00 | admin     | admin      | 2025-09-01 10:05:00  | 1       |
| 2   | Order Number | A unique identifier assigned to each order placed by a customer.           | E-Commerce| Invoice     | false      | 2025-09-01 11:00:00 | 2025-09-01 11:00:00 | user1     | null       | null                 | 1       |
| 3   | Job Order    | A request for the performance of work or the provision of services.        | Automotive | Customer Services     | false      | 2025-09-01 12:00:00 | 2025-09-01 12:00:00 | user2     | null       | null                 | 1       |
| 4   | Service Advisory | A formal recommendation provided to a customer regarding services.        | Automotive | Maintenance     | false      | 2025-09-01 13:00:00 | 2025-09-01 13:00:00 | user3     | null       | null                 | 1       |
| 5   | Invoice Date | The date when an invoice is issued to a customer for payment.              | Finance | Invoice     | true       | 2025-09-01 14:00:00 | 2025-09-01 14:00:00 | admin     | admin      | 2025-09-01 14:05:00  | 1       |

## Core Features

- **Add Term**: A user can add a new term. Newly added terms are unapproved by default.
- **Approve Term**: An authorized user can approve unapproved terms.
- **List Terms**: A user can list, update, or delete the terms they added. (Approved terms cannot be deleted or updated.)

## Development Milestones

For version `1.0.0.0`:

- Without a user and role management system:
  - Implement the `Add Term` functionality.
  - On the `List Terms` page, display, filter, and sort the added terms.
  - On the `List Terms` page, enable delete and update operations.

- For version `1.0.0.1`:
  - Use the left menu on the Layout page.
  - Use Bootstrap 5, not custom CSS.
  - Rearrange the Privacy and Index pages to suit the project's purpose.
  - Use Awesome Fonts icons where appropriate.
  - Show total term and domain counts on the Index page.
  - Show the last 5 added terms on the Index page.

> This document is created with the assistance of AI (GPT 5, 1x) and reviewed by a human.