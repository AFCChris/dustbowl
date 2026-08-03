#!/bin/bash
set -e

# Source files are authoritative; refresh generated app and artifact outputs
# after every task merge so the preview never serves stale code.
npm run build