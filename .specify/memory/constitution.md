<!--
  Sync Impact Report
  ==================
  Version change: N/A → 1.0.0 (initial ratification)
  Modified principles: N/A (initial creation)
  Added sections:
    - Core Principles (5 principles: DDD, TDD, UX Consistency, Security, Performance)
    - Additional Constraints (technology and compliance standards)
    - Development Workflow (review process and quality gates)
    - Governance (amendment procedure and versioning policy)
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ compatible (Constitution Check section already present)
    - .specify/templates/spec-template.md ✅ compatible (requirements and success criteria align)
    - .specify/templates/tasks-template.md ✅ compatible (phase structure supports TDD and story-driven delivery)
    - .specify/templates/checklist-template.md ✅ compatible (generic checklist supports principle-driven items)
  Follow-up TODOs: None
-->

# My Assistant Constitution

## Core Principles

### I. Domain-Driven Design (DDD)

- All features MUST be modeled around the business domain, not
  around technical concerns or infrastructure layers.
- Bounded Contexts MUST be explicitly defined and documented before
  implementation begins. Each context owns its models, services, and
  persistence logic.
- Ubiquitous Language MUST be established per Bounded Context and
  used consistently in code identifiers, documentation, tests, and
  team communication. Deviations are treated as bugs.
- Aggregates MUST enforce invariants at write boundaries. No external
  code may modify aggregate internals directly.
- Domain logic MUST reside in the domain layer. Application services
  orchestrate use cases; infrastructure adapters handle I/O. Leaking
  domain logic into controllers, handlers, or persistence code is
  prohibited.
- New modules MUST map to a single Bounded Context. Cross-context
  communication MUST use explicit integration events or
  Anti-Corruption Layers — never direct model imports.

### II. Test-Driven Development (TDD) (NON-NEGOTIABLE)

- TDD is mandatory for all production code. The Red-Green-Refactor
  cycle MUST be strictly followed:
  1. Write a failing test that defines the desired behavior.
  2. Implement the minimum code to make the test pass.
  3. Refactor while keeping all tests green.
- Tests MUST be written and confirmed to fail BEFORE any
  implementation code is written. Pull requests that add production
  code without corresponding tests MUST be rejected.
- Test categories and their requirements:
  - **Unit tests**: MUST cover every public method in domain and
    application layers. MUST run in isolation with no I/O.
  - **Integration tests**: MUST verify Bounded Context boundaries,
    persistence contracts, and external service adapters.
  - **Contract tests**: MUST validate API contracts between services
    and between frontend/backend boundaries.
- Test names MUST describe the behavior under test, not the method
  name (e.g., `should_reject_order_when_inventory_insufficient`,
  not `test_create_order`).
- Code coverage MUST NOT drop below the project baseline. New code
  MUST achieve ≥90% branch coverage.

### III. User Experience Consistency

- All user-facing interfaces MUST follow a single, documented design
  system. Ad-hoc styling or component creation outside the design
  system is prohibited.
- Interaction patterns (navigation, forms, feedback, error states)
  MUST be consistent across all features and surfaces. Users MUST
  NOT encounter different paradigms for equivalent actions.
- Every user action MUST provide immediate, perceptible feedback
  (loading indicators, success confirmations, inline error messages).
  Silent failures are prohibited.
- Accessibility MUST meet WCAG 2.1 AA as a minimum. Keyboard
  navigation, screen-reader compatibility, and sufficient color
  contrast MUST be verified before any UI is considered complete.
- Error messages MUST be user-friendly, actionable, and free of
  technical jargon. They MUST state what happened, why, and what the
  user can do next.
- UX changes MUST be validated against the documented design system
  and approved by design review before merging.

### IV. Security-First

- All external input MUST be validated and sanitized at the system
  boundary before processing. No unvalidated data may reach domain
  logic.
- Authentication and authorization MUST be enforced on every
  endpoint and operation. Default policy is deny-all; access MUST
  be explicitly granted.
- Secrets (API keys, credentials, tokens) MUST NEVER appear in
  source code, logs, or error messages. All secrets MUST be managed
  through a dedicated secrets manager or environment-variable
  injection.
- Dependencies MUST be audited for known vulnerabilities before
  adoption and on every CI build. Critical/High CVEs MUST be
  resolved within 48 hours of detection.
- Data at rest and in transit MUST be encrypted. TLS 1.2+ is the
  minimum for all network communication.
- Security-relevant events (login attempts, permission changes, data
  access) MUST be logged with sufficient detail for audit and
  incident response, without logging sensitive data values.
- A threat model MUST be documented for any new feature that
  introduces new attack surfaces (new endpoints, new data stores,
  new external integrations).

### V. Performance & Efficiency

- Every feature MUST define measurable performance budgets before
  implementation (response time, throughput, memory footprint).
  Budgets MUST be documented in the feature plan.
- API responses MUST complete within 200ms at p95 under expected
  load unless the feature plan explicitly documents and justifies
  a higher threshold.
- UI interactions MUST render within 100ms for user-perceived
  responsiveness. Initial page load MUST complete within 2 seconds
  on a standard connection.
- Database queries MUST be reviewed for N+1 problems, missing
  indexes, and unbounded result sets. Every query introduced MUST
  include an execution plan review.
- Performance regression tests MUST be included in CI for critical
  paths. Any measured regression exceeding 10% from the baseline
  MUST block the release until resolved or explicitly approved.
- Resource consumption (CPU, memory, connections) MUST be profiled
  for new features. Unbounded growth patterns (memory leaks,
  connection pool exhaustion) MUST be caught in testing.

## Additional Constraints

- **Technology decisions** MUST be documented with rationale in
  Architecture Decision Records (ADRs). Adopting a new framework,
  library, or infrastructure component requires an ADR approved
  before implementation begins.
- **Third-party dependencies** MUST be evaluated for license
  compatibility, maintenance health (last release, open issues,
  bus factor), and security posture before adoption.
- **Environment parity**: Development, staging, and production
  environments MUST be structurally equivalent. Infrastructure MUST
  be defined as code (IaC) and version-controlled.
- **Data handling** MUST comply with applicable privacy regulations
  (e.g., GDPR, CCPA). Personal data processing MUST be documented
  and minimized to what is strictly necessary.

## Development Workflow

- **Branching**: All work MUST be done on feature branches. Direct
  commits to the main branch are prohibited.
- **Code review**: Every pull request MUST be reviewed by at least
  one team member who did not author the change. Reviewers MUST
  verify compliance with all constitution principles.
- **Constitution compliance gate**: PR review checklists MUST include
  explicit checks for DDD adherence, TDD compliance, UX consistency,
  security posture, and performance budget conformance.
- **CI pipeline**: All tests (unit, integration, contract), linting,
  security scans, and performance benchmarks MUST pass before a PR
  can be merged. Failures MUST block merge.
- **Documentation**: Public APIs, domain models, and architectural
  decisions MUST be documented at the time of implementation, not
  deferred. Documentation is a merge requirement.
- **Commit discipline**: Commits MUST be atomic and descriptive.
  Each commit SHOULD represent a single logical change. Squash
  commits that solely fix lint/typo issues introduced in the
  same PR.

## Governance

- This constitution is the supreme governance document for the
  My Assistant project. It supersedes all other practices, guides,
  and conventions where conflicts arise.
- **Amendment procedure**: Any team member may propose an amendment.
  Proposals MUST include: the specific change, rationale, and an
  impact assessment on existing code and processes. Amendments MUST
  be reviewed and approved before taking effect.
- **Versioning policy**: The constitution follows semantic versioning:
  - MAJOR: Backward-incompatible governance changes (principle
    removal or fundamental redefinition).
  - MINOR: New principle or section added, or material expansion
    of existing guidance.
  - PATCH: Clarifications, wording fixes, non-semantic refinements.
- **Compliance review**: Constitution compliance MUST be verified
  during every code review. Quarterly reviews SHOULD assess whether
  principles remain relevant and whether new principles are needed.
- **Conflict resolution**: When a principle conflicts with a
  pragmatic constraint (e.g., deadline pressure), the conflict MUST
  be documented as a Complexity Tracking entry with explicit
  justification and a remediation plan.

**Version**: 1.0.0 | **Ratified**: 2026-02-23 | **Last Amended**: 2026-02-23
