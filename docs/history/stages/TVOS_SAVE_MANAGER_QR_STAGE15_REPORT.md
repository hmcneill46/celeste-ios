# Stage 15 — Save Manager QR pairing

## Scope

Stage 15 is the first post-`v1.0.0-rc.1` feature. It was developed on
`feature/save-manager-qr` from RC commit
`ee52b0868df091746f134d95d4f020f94f23d4fb`. The tag and `tvos-port` integration
branch remain immutable during this stage.

## Architecture

- Apple's bound Core Image `CIQRCodeGenerator` creates the QR module matrix.
- Correction level **M** is used. A repository-owned renderer adds an exact
  four-module quiet zone and expands modules by an integer scale into an
  opaque RGBA texture of at most 420 × 420 pixels.
- The QR targets the same ready `NWListener` and highest-ranked numeric LAN URL
  shown by Save Manager. It is created only after the dynamic port and usable
  address are known.
- The encoded form is `http://<numeric-address>:<port>/pair#<credential>`. The
  fragment is not sent by the browser's initial request. A tiny nonce-protected
  local bootstrap removes it from the visible URL and submits it to fixed
  `POST /pair`.
- The pairing credential contains 256 bits from the platform cryptographic RNG,
  is memory-only, expires after three minutes, and is consumed atomically by
  the first successful pairing. It is distinct from the human access code,
  authenticated session, CSRF token, and revision value.
- Successful pairing issues the existing random `HttpOnly; SameSite=Strict`
  session cookie. All existing authentication lifetime, CSRF, revision,
  serializer, mutation, and soft-reload boundaries are reused unchanged.
- Manual numeric URL plus six-digit access-code authentication remains a full
  fallback and can authenticate another browser after the QR has been used.

No external resources, third-party QR dependency, camera permission, cloud
service, new entitlement, persistence key, or persistence-format change was
introduced. Every accepted Save Manager stop path invalidates the pairing
credential and clears rendered QR state.

## Verification

- Stage 15 deterministic pairing tests: **31 passed**.
- Existing Stage 10 protocol/security tests: **66 passed unchanged**.
- Existing Stage 11, Stage 12B, and Stage 13B suites passed unchanged: **38**,
  **16**, and **20** tests respectively. The Stage 9B and frozen Stage 14
  verifier chains also passed.
- Release `tvos-arm64` with full trimming, full AOT, and
  `UseInterpreter=false` built successfully. The signed same-identity app
  launched on the physical Apple TV, restored the accepted product state, and
  retained one FNA/FMOD runtime through the tested mutation and soft reload.
- The signing-ready unsigned IPA is **895,595,375 bytes** with SHA-256
  `2ab1fd6cad93a081c7ba7068d21f276c7ccc5eec78a9ac247411fb40f341a9f7`.
  Package verification found the conventional unsigned payload, seven banks,
  Stage 15 product tokens, no provisioning profile, and no usable signature.
- An independent empty candidate worktree initialized every recursive
  submodule, passed builder help and host doctor, validated the exact accepted
  Celeste/FMOD inputs, rebuilt Stage 1 without copied output, reproduced logical
  SHA-256 `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
  generated the managed product, and built/verified the unsigned IPA above.
  Its Git worktree remained clean because all generated and proprietary output
  stayed ignored.
- Credential values, access codes, LAN addresses, cookies, and CSRF tokens are
  excluded from routine logs and this report.

## Final acceptance evidence

- Core Image produced a 43-module symbol for the accepted IPv4 pairing URL.
  The renderer added the required four-module quiet zone and used an integer
  scale of eight, producing a crisp **408 x 408 pixel** on-TV image at correction
  level **M**.
- A real iPhone Camera recognized the QR from approximately three metres from a
  55-inch television and opened Safari directly into the authenticated Save
  Manager without asking for the six-digit code. The observed size and contrast
  were comfortable throughout reasonable living-room viewing distances.
- A fresh physical QR credential was retained only in ignored local evidence.
  Its first fixed-route submission created one normal authenticated session and
  reached the manager; immediate reuse from a separate session returned 401 and
  created no second cookie. The manual six-digit path remained available.
- Expiration was covered by the injected-clock deterministic test and observed
  physically: after three minutes the TV removed the QR and instructed the user
  to use the still-visible address and access code. No crash or stale image
  remained.
- The QR-authenticated browser downloaded Settings and performed a controlled
  Settings reset. The TV presented the existing Confirm-driven reload flow;
  Confirm returned Celeste to a functional menu with normal runtime behaviour.
- On a separate fresh activation, the user deliberately ignored the QR, typed
  the displayed numeric URL, entered the six-digit code, reached the existing
  authenticated manager, and downloaded a file successfully. QR pairing did
  not regress the manual fallback.
- A new clone of the actual pushed feature branch supplies the final
  remote-branch presentation and cleanliness check.

## Known limitations

The QR opens an ordinary HTTP service available only on the same LAN while the
foreground Save Manager is active. A phone that cannot scan or open that local
URL must use the always-visible manual URL and six-digit code.
