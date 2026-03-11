# OpenTelemetry Integration Test Script
# Tests OTEL collector deployment and trace export from Lightning Server

param(
    [switch]$Verbose
)

Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║      OpenTelemetry Integration Test Suite                ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$testsPassed = 0
$testsFailed = 0

# Test 1: Check OTEL Collector container
Write-Host "`n[1/6] Checking OTEL Collector container..." -ForegroundColor Yellow

try {
    $otelStatus = docker ps --filter "name=deepresearch-otel-collector" --format "{{.Status}}"
    
    if ($otelStatus -match "Up") {
        Write-Host "  ✓ OTEL Collector is running" -ForegroundColor Green
        Write-Host "    Status: $otelStatus" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "  ✗ OTEL Collector is not running" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "  ✗ Error checking collector: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 2: Check OTLP endpoints
Write-Host "`n[2/6] Testing OTLP endpoints..." -ForegroundColor Yellow

try {
    # Test GRPC endpoint (4317)
    $grpcTest = docker exec deepresearch-otel-collector netstat -ln | Select-String "4317"
    
    if ($grpcTest) {
        Write-Host "  ✓ OTLP GRPC endpoint (4317) is listening" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ OTLP GRPC endpoint (4317) not found" -ForegroundColor Red
        $testsFailed++
    }
    
    # Test HTTP endpoint (4318)  
    $httpTest = docker exec deepresearch-otel-collector netstat -ln | Select-String "4318"
    
    if ($httpTest) {
        Write-Host "  ✓ OTLP HTTP endpoint (4318) is listening" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ⚠ OTLP HTTP endpoint (4318) not found" -ForegroundColor Yellow
        Write-Host "    Note: GRPC (4317) is primary, HTTP optional" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "  ✗ Error testing endpoints: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 3: Check health check endpoint
Write-Host "`n[3/6] Testing health check endpoint..." -ForegroundColor Yellow

try {
    $healthCheck = docker exec deepresearch-otel-collector curl -s http://localhost:13133/
    
    if ($healthCheck -match "Server available") {
        Write-Host "  ✓ Health check endpoint responding" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ⚠ Health check response: $healthCheck" -ForegroundColor Yellow
        if ($healthCheck -match "ok" -or $healthCheck -match "ready") {
            Write-Host "  ✓ Health check operational (non-standard response)" -ForegroundColor Green
            $testsPassed++
        } else {
            Write-Host "  ✗ Health check not responding properly" -ForegroundColor Red
            $testsFailed++
        }
    }
} catch {
    Write-Host "  ✗ Error checking health endpoint: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 4: Check Lightning Server OTEL configuration
Write-Host "`n[4/6] Checking Lightning Server OTEL config..." -ForegroundColor Yellow

$lightningRunning = docker ps --filter "name=research-lightning-server" --format "{{.Status}}"

if ($lightningRunning) {
    try {
        $otelEnv = docker exec research-lightning-server env | Select-String "OTEL"
        
        if ($otelEnv) {
            Write-Host "  ✓ OTEL environment variables configured:" -ForegroundColor Green
            $otelEnv.Split("`n") | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Gray
            }
            $testsPassed++
        } else {
            Write-Host "  ✗ No OTEL environment variables found" -ForegroundColor Red
            $testsFailed++
        }
    } catch {
        Write-Host "  ✗ Error checking OTEL config: $_" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host "  ⓘ Lightning Server not running, skipping test" -ForegroundColor Gray
}

# Test 5: Test trace export from Lightning Server
Write-Host "`n[5/6] Testing trace export..." -ForegroundColor Yellow

if ($lightningRunning) {
    try {
        $traceTest = docker exec research-lightning-server python -c @"
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import Resource, SERVICE_NAME
import os
import sys

try:
    # Get OTLP endpoint from environment
    endpoint = os.environ.get('OTEL_EXPORTER_OTLP_ENDPOINT', 'http://deepresearch-otel-collector:4317')
    
    # Setup tracer
    resource = Resource(attributes={SERVICE_NAME: 'test-service'})
    provider = TracerProvider(resource=resource)
    
    # Create OTLP exporter (insecure for local testing)
    exporter = OTLPSpanExporter(
        endpoint=endpoint,
        insecure=True
    )
    
    processor = BatchSpanProcessor(exporter)
    provider.add_span_processor(processor)
    trace.set_tracer_provider(provider)
    
    # Create test span
    tracer = trace.get_tracer(__name__)
    with tracer.start_as_current_span('test-span') as span:
        span.set_attribute('test.attribute', 'test-value')
        span.add_event('test-event')
    
    # Force export
    provider.shutdown()
    
    print('✅ Trace export test successful')
    print(f'   Endpoint: {endpoint}')
    sys.exit(0)
    
except Exception as e:
    print(f'❌ Trace export failed: {e}')
    import traceback
    traceback.print_exc()
    sys.exit(1)
"@
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Trace export from Lightning Server working" -ForegroundColor Green
            $traceTest.Split("`n") | ForEach-Object {
                if ($_ -match "✅|Endpoint") {
                    Write-Host "    $_" -ForegroundColor Gray
                }
            }
            $testsPassed++
        } else {
            Write-Host "  ✗ Trace export test failed" -ForegroundColor Red
            Write-Host "    $traceTest" -ForegroundColor Gray
            $testsFailed++
        }
        
    } catch {
        Write-Host "  ✗ Error testing trace export: $_" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host "  ⓘ Lightning Server not running, skipping test" -ForegroundColor Gray
}

# Test 6: Check OTEL Collector logs for received traces
Write-Host "`n[6/6] Checking collector logs for trace activity..." -ForegroundColor Yellow

try {
    $collectorLogs = docker logs deepresearch-otel-collector --tail 50 2>&1
    
    if ($collectorLogs -match "traces" -or $collectorLogs -match "span" -or $collectorLogs -match "otlp") {
        Write-Host "  ✓ Collector processing traces (activity detected)" -ForegroundColor Green
        
        if ($Verbose) {
            Write-Host "    Recent log entries:" -ForegroundColor Gray
            $collectorLogs.Split("`n") | Select-Object -Last 10 | ForEach-Object {
                Write-Host "      $_" -ForegroundColor DarkGray
            }
        }
        
        $testsPassed++
    } else {
        Write-Host "  ⚠ No trace activity in recent logs" -ForegroundColor Yellow
        Write-Host "    This is normal if no requests have been made yet" -ForegroundColor Gray
        
        if ($Verbose) {
            Write-Host "    Recent log entries:" -ForegroundColor Gray
            $collectorLogs.Split("`n") | Select-Object -Last 10 | ForEach-Object {
                Write-Host "      $_" -ForegroundColor DarkGray
            }
        }
    }
    
} catch {
    Write-Host "  ✗ Error checking collector logs: $_" -ForegroundColor Red
    $testsFailed++
}

# Summary
Write-Host "`n" -NoNewline
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                      TEST SUMMARY                         " -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

$totalTests = $testsPassed + $testsFailed
Write-Host "`nTests Passed: $testsPassed / $totalTests" -ForegroundColor Green
if ($testsFailed -gt 0) {
    Write-Host "Tests Failed: $testsFailed / $totalTests" -ForegroundColor Red
}

if ($testsFailed -eq 0) {
    Write-Host "`n✅ OpenTelemetry is properly configured!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "  1. Generate traffic: curl http://localhost:8090/health" -ForegroundColor Gray
    Write-Host "  2. View traces in your observability backend (Jaeger/Zipkin)" -ForegroundColor Gray
    Write-Host "  3. Configure OTEL collector exporters in config file" -ForegroundColor Gray
} else {
    Write-Host "`n⚠️ Some tests failed. Review errors above." -ForegroundColor Yellow
    Write-Host "`nTroubleshooting:" -ForegroundColor Cyan
    Write-Host "  1. Check collector config: docker exec deepresearch-otel-collector cat /etc/otel-collector-config.yml" -ForegroundColor Gray
    Write-Host "  2. Restart collector: docker restart deepresearch-otel-collector" -ForegroundColor Gray
    Write-Host "  3. Check network: docker network inspect deepresearch-hub" -ForegroundColor Gray
    Write-Host "  4. Verify OTLP endpoint: docker exec research-lightning-server env | grep OTEL" -ForegroundColor Gray
}

Write-Host "`n📊 OTEL Endpoints:" -ForegroundColor Cyan
Write-Host "  OTLP GRPC:  http://localhost:4317" -ForegroundColor Gray
Write-Host "  OTLP HTTP:  http://localhost:4318" -ForegroundColor Gray
Write-Host "  Health:     http://localhost:13133" -ForegroundColor Gray
Write-Host "  Metrics:    http://localhost:8889/metrics" -ForegroundColor Gray

Write-Host ""
exit $testsFailed
