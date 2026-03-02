#!/bin/bash

################################################################################
# DeepResearch Docker Compose Stack Manager
# Unified deployment automation for 4-stack architecture
#
# Usage:
#   ./deploy-stacks.sh [ACTION] [STACK] [OPTIONS]
#
# Actions:
#   start      - Start specified stack(s)
#   stop       - Stop specified stack(s)
#   restart    - Restart specified stack(s)
#   status     - Show stack status
#   health     - Check stack health
#   logs       - Show stack logs
#   validate   - Validate deployment
#   cleanup    - Remove all stacks and network
#
# Stacks:
#   all        - All stacks (core, ai, websearch, monitoring)
#   core       - Core services (API, Redis, InfluxDB)
#   ai         - AI services (Ollama, Qdrant, Lightning)
#   websearch  - Websearch services (SearXNG, Crawl4AI, Caddy)
#   monitoring - Monitoring stack (Prometheus, Grafana, etc)
#
# Options:
#   -f, --follow           - Follow logs continuously
#   -t, --tail N           - Show last N log lines (default: 50)
#   -v, --verbose          - Verbose output
#   -c, --create-missing   - Create placeholder docker-compose files if missing
#
# Examples:
#   ./deploy-stacks.sh start all           # Start all stacks
#   ./deploy-stacks.sh status core         # Show core stack status
#   ./deploy-stacks.sh logs -f monitoring  # Follow monitoring logs
#   ./deploy-stacks.sh start all -c        # Create missing files and start
#
# Author: DeepResearch Architecture Team
# Version: 1.0
# Date: March 2, 2026
################################################################################

set -euo pipefail

# ═══════════════════════════════════════════════════════════════════════════════
# CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Network configuration
NETWORK_NAME="deepresearch-hub"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Options
ACTION="${1:-status}"
STACK="${2:-all}"
FOLLOW=false
TAIL=50
VERBOSE=false
CREATE_MISSING=false

# Parse additional options
shift 2 || true
while [[ $# -gt 0 ]]; do
    case $1 in
        -f|--follow)
            FOLLOW=true
            shift
            ;;
        -t|--tail)
            TAIL="$2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -c|--create|--create-missing)
            CREATE_MISSING=true
            shift
            ;;
        *)
            shift
            ;;
    esac
done

# ═══════════════════════════════════════════════════════════════════════════════
# STACK DEFINITIONS
# ═══════════════════════════════════════════════════════════════════════════════

declare -A STACKS=(
    [core]="Core Services|Docker/docker-compose.core.yml"
    [ai]="AI Services|Docker/docker-compose.ai.yml"
    [websearch]="Websearch Services|Docker/docker-compose.websearch.yml"
    [monitoring]="Monitoring Stack|Docker/Observability/docker-compose-monitoring.yml"
)

declare -A SERVICES=(
    [core]="deepresearch-api deep-research-agent redis influxdb redis-exporter"
    [ai]="ollama qdrant lightning-server"
    [websearch]="caddy searxng crawl4ai"
    [monitoring]="deepresearch-prometheus deepresearch-grafana deepresearch-alertmanager deepresearch-jaeger deepresearch-otel-collector"
)

declare -A DEPENDENCIES=(
    [core]=""
    [ai]="core"
    [websearch]="core"
    [monitoring]="core ai websearch"
)

# ═══════════════════════════════════════════════════════════════════════════════
# UTILITY FUNCTIONS
# ═══════════════════════════════════════════════════════════════════════════════

write_status() {
    local message="$1"
    local type="${2:-info}"

    case "$type" in
        success)
            echo -e "${GREEN}[OK] ${message}${NC}"
            ;;
        error)
            echo -e "${RED}[ERROR] ${message}${NC}"
            ;;
        warning)
            echo -e "${YELLOW}[WARN] ${message}${NC}"
            ;;
        info)
            echo -e "${CYAN}[INFO] ${message}${NC}"
            ;;
        header)
            echo -e "${MAGENTA}== ${message}${NC}"
            ;;
    esac
}

write_header() {
    local title="$1"
    echo ""
    echo -e "${MAGENTA}+====================================================================+${NC}"
    printf "${MAGENTA}| %-61s |${NC}\n" "$title"
    echo -e "${MAGENTA}+====================================================================+${NC}"
    echo ""
}

test_network_exists() {
    docker network ls --filter "name=$NETWORK_NAME" --format "{{.Name}}" 2>/dev/null | grep -q "$NETWORK_NAME"
    return $?
}

test_compose_file_exists() {
    local file_path="$1"
    [ -f "$file_path" ]
    return $?
}

test_all_compose_files_exist() {
    local -a stacks_to_check

    if [ "$STACK" = "all" ]; then
        stacks_to_check=("core" "ai" "websearch" "monitoring")
    else
        stacks_to_check=("$STACK")
    fi

    local has_missing=0

    for stack in "${stacks_to_check[@]}"; do
        local compose_file=$(get_compose_file "$stack")
        local full_path="$PROJECT_ROOT/$compose_file"

        if ! test_compose_file_exists "$full_path"; then
            echo "$stack|$compose_file|$full_path"
            has_missing=1
        fi
    done

    return $has_missing
}

show_missing_files_help() {
    local has_missing=0

    # Check if there are missing files
    for stack in "${!STACKS[@]}"; do
        local compose_file=$(get_compose_file "$stack")
        local full_path="$PROJECT_ROOT/$compose_file"

        if ! test_compose_file_exists "$full_path"; then
            has_missing=1
            break
        fi
    done

    if [ $has_missing -eq 0 ]; then
        return 0
    fi

    echo ""
    echo -e "${YELLOW}+====================================================================+${NC}"
    echo -e "${YELLOW}|                    MISSING DOCKER COMPOSE FILES                |${NC}"
    echo -e "${YELLOW}+====================================================================+${NC}"
    echo ""

    for stack in "${!STACKS[@]}"; do
        local compose_file=$(get_compose_file "$stack")
        local full_path="$PROJECT_ROOT/$compose_file"

        if ! test_compose_file_exists "$full_path"; then
            write_status "Missing: $compose_file" "error"
            echo -e "  ${CYAN}Path: $full_path${NC}"
        fi
    done

    echo ""
    echo -e "${YELLOW}[WARN]  The following docker-compose files are required but not found:${NC}"
    echo ""
    echo -e "${CYAN}  Option 1: Provide the missing docker-compose files in the Docker directory${NC}"
    echo ""
    echo -e "${CYAN}  Option 2: Create placeholder files with this script:${NC}"
    echo -e "${GREEN}    ./deploy-stacks.sh start all --create-missing${NC}"
    echo ""
    echo -e "${CYAN}  Option 3: Generate files manually (examples available in Docker/examples/)${NC}"
    echo ""
}

create_placeholder_file() {
    local stack="$1"
    local full_path="$2"
    local dir=$(dirname "$full_path")

    mkdir -p "$dir"

    cat > "$full_path" << EOF
version: '3.8'

services:
  # Placeholder for $stack stack
  # TODO: Add service definitions

networks:
  deepresearch-hub:
    external: true
    driver: bridge
EOF

    write_status "Created placeholder: $(basename "$full_path")" "success"
}

start_interactive_file_creation() {
    echo ""
    echo -e "${CYAN}Would you like to create placeholder docker-compose files? (y/n)${NC}"
    read -r response

    if [ "$response" = "y" ] || [ "$response" = "yes" ]; then
        # List of stacks to create files for
        local stacks=("core" "ai" "websearch" "monitoring")

        for stack in "${stacks[@]}"; do
            local compose_file=$(get_compose_file "$stack")
            local full_path="$PROJECT_ROOT/$compose_file"

            create_placeholder_file "$stack" "$full_path"
        done

        echo ""
        write_status "Placeholder files created. Please edit them with proper service definitions." "warning"
        echo ""
        return 0
    fi

    return 1
}

create_network() {
    if ! test_network_exists; then
        write_status "Creating unified network: $NETWORK_NAME" "info"
        if docker network create "$NETWORK_NAME" --driver bridge >/dev/null 2>&1; then
            write_status "Network created successfully" "success"
            return 0
        else
            write_status "Failed to create network" "error"
            return 1
        fi
    else
        write_status "Network already exists: $NETWORK_NAME" "info"
        return 0
    fi
}

get_compose_file() {
    local stack="$1"
    IFS='|' read -r name file <<< "${STACKS[$stack]}"
    echo "$file"
}

get_stack_name() {
    local stack="$1"
    IFS='|' read -r name file <<< "${STACKS[$stack]}"
    echo "$name"
}

start_stack() {
    local stack="$1"
    local name=$(get_stack_name "$stack")
    local compose_file=$(get_compose_file "$stack")
    
    write_status "Starting $name..." "info"
    
    if cd "$PROJECT_ROOT" && docker-compose -f "$compose_file" up -d 2>/dev/null; then
        write_status "$name started" "success"
        return 0
    else
        write_status "Failed to start $name" "error"
        return 1
    fi
}

stop_stack() {
    local stack="$1"
    local name=$(get_stack_name "$stack")
    local compose_file=$(get_compose_file "$stack")
    
    write_status "Stopping $name..." "info"
    
    if cd "$PROJECT_ROOT" && docker-compose -f "$compose_file" down 2>/dev/null; then
        write_status "$name stopped" "success"
        return 0
    else
        write_status "Failed to stop $name" "error"
        return 1
    fi
}

get_stack_status() {
    local stack="$1"
    local name=$(get_stack_name "$stack")
    
    write_status "$name" "header"
    
    read -ra service_list <<< "${SERVICES[$stack]}"
    
    for service in "${service_list[@]}"; do
        local status=$(docker ps -a --filter "name=$service" --format "{{.Names}}\t{{.Status}}" 2>/dev/null || echo "")
        
        if [ -z "$status" ]; then
            write_status "$service → NOT RUNNING" "error"
        else
            local container=$(echo "$status" | cut -f1)
            local state=$(echo "$status" | cut -f2-)
            
            if [[ "$state" == *"Up"* ]]; then
                write_status "$container → $state" "success"
            elif [[ "$state" == *"Exited"* ]]; then
                write_status "$container → $state" "error"
            else
                write_status "$container → $state" "warning"
            fi
        fi
    done
}

get_stack_health() {
    local stack="$1"
    local name=$(get_stack_name "$stack")
    
    write_status "$name" "header"
    
    read -ra service_list <<< "${SERVICES[$stack]}"
    
    local healthy=0
    local total=${#service_list[@]}
    
    for service in "${service_list[@]}"; do
        local status=$(docker ps --filter "name=$service" --format "{{.Status}}" 2>/dev/null || echo "")
        
        if [ -z "$status" ]; then
            write_status "$service → NOT RUNNING" "error"
        elif [[ "$status" == *"healthy"* ]]; then
            write_status "$service → HEALTHY" "success"
            ((healthy++))
        elif [[ "$status" == *"unhealthy"* ]]; then
            write_status "$service → UNHEALTHY" "warning"
        elif [[ "$status" == *"health: starting"* ]]; then
            write_status "$service → STARTING" "info"
        else
            write_status "$service → UP (no health check)" "info"
            ((healthy++))
        fi
    done
    
    if [ $healthy -eq $total ]; then
        write_status "Health: $healthy/$total services" "success"
    else
        write_status "Health: $healthy/$total services" "warning"
    fi
}

show_stack_logs() {
    local stack="$1"
    local name=$(get_stack_name "$stack")
    local compose_file=$(get_compose_file "$stack")
    
    write_status "Showing logs for $name" "info"
    echo ""
    
    cd "$PROJECT_ROOT"
    
    if [ "$FOLLOW" = true ]; then
        docker-compose -f "$compose_file" logs -f --tail="$TAIL"
    else
        docker-compose -f "$compose_file" logs --tail="$TAIL"
    fi
}

sort_stacks_forward() {
    local -a input=("$@")
    local -a output=()
    
    # Define order
    local -a order=("core" "ai" "websearch" "monitoring")
    
    for stack_name in "${order[@]}"; do
        for item in "${input[@]}"; do
            if [ "$item" = "$stack_name" ]; then
                output+=("$item")
            fi
        done
    done
    
    printf '%s\n' "${output[@]}"
}

sort_stacks_reverse() {
    local -a input=("$@")
    local -a output=()
    
    # Define order (reverse)
    local -a order=("monitoring" "websearch" "ai" "core")
    
    for stack_name in "${order[@]}"; do
        for item in "${input[@]}"; do
            if [ "$item" = "$stack_name" ]; then
                output+=("$item")
            fi
        done
    done
    
    printf '%s\n' "${output[@]}"
}

get_stacks_to_process() {
    if [ "$STACK" = "all" ]; then
        echo "core ai websearch monitoring"
    else
        echo "$STACK"
    fi
}

validate_deployment() {
    write_header "Validating Deployment"
    
    # Check Docker
    write_status "Checking Docker daemon..." "info"
    if docker version >/dev/null 2>&1; then
        write_status "Docker daemon is running" "success"
    else
        write_status "Docker daemon is not running" "error"
        return 1
    fi
    
    # Check network
    write_status "Checking unified network..." "info"
    if test_network_exists; then
        write_status "Network $NETWORK_NAME exists" "success"
    else
        write_status "Network $NETWORK_NAME not found" "error"
        return 1
    fi
    
    # Check services
    write_status "Checking all services..." "info"
    local running=0
    local total=0
    
    for stack in "${!SERVICES[@]}"; do
        read -ra service_list <<< "${SERVICES[$stack]}"
        for service in "${service_list[@]}"; do
            ((total++))
            if docker ps --filter "name=$service" --format "{{.Names}}" 2>/dev/null | grep -q "$service"; then
                ((running++))
            fi
        done
    done
    
    if [ $running -eq $total ]; then
        write_status "$running/$total services running" "success"
    else
        write_status "$running/$total services running" "warning"
    fi
    
    return 0
}

cleanup_stack() {
    write_header "Cleaning Up DeepResearch Stack"
    
    read -p "This will remove all containers, volumes, and the network. Continue? (yes/no): " -r response
    
    if [ "$response" != "yes" ]; then
        write_status "Cleanup cancelled" "warning"
        return
    fi
    
    # Stop stacks in reverse order
    local stacks_to_stop=$(sort_stacks_reverse $(get_stacks_to_process))
    
    while read -r stack; do
        stop_stack "$stack"
        sleep 2
    done <<< "$stacks_to_stop"
    
    # Remove network
    if test_network_exists; then
        write_status "Removing network: $NETWORK_NAME" "info"
        docker network rm "$NETWORK_NAME" 2>/dev/null
        write_status "Network removed" "success"
    fi
    
    write_status "Cleanup completed" "success"
}

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -f|--follow)
                FOLLOW=true
                shift
                ;;
            -t|--tail)
                TAIL="$2"
                shift 2
                ;;
            -v|--verbose)
                VERBOSE=true
                shift
                ;;
            *)
                shift
                ;;
        esac
    done
}

# Check if any compose files are missing
check_missing_compose_files() {
    for stack in "${!STACKS[@]}"; do
        local compose_file=$(get_compose_file "$stack")
        local full_path="$PROJECT_ROOT/$compose_file"

        if ! test_compose_file_exists "$full_path"; then
            return 1  # Has missing files
        fi
    done

    return 0  # All files exist
}

# ═══════════════════════════════════════════════════════════════════════════════
# MAIN EXECUTION
# ═══════════════════════════════════════════════════════════════════════════════

write_header "DeepResearch Stack Manager v1.0"

# Check for missing compose files
if ! check_missing_compose_files; then
    show_missing_files_help

    if [ "$CREATE_MISSING" = true ]; then
        if start_interactive_file_creation; then
            write_status "Please edit the placeholder files and run the script again" "warning"
            exit 0
        fi
    fi

    write_status "Cannot proceed without docker-compose files" "error"
    echo -e "  ${CYAN}Use --create-missing flag to create placeholder files:${NC}"
    echo -e "  ${GREEN}./deploy-stacks.sh start all --create-missing${NC}"
    exit 1
fi

stacks_to_process=$(get_stacks_to_process)

case "$ACTION" in
    start)
        if ! create_network; then
            write_status "Cannot proceed without network" "error"
            exit 1
        fi
        
        write_header "Starting Stacks"
        
        sorted_stacks=$(sort_stacks_forward $stacks_to_process)
        while read -r stack; do
            start_stack "$stack"
            sleep 5
        done <<< "$sorted_stacks"
        
        write_status "All requested stacks started" "success"
        ;;
    
    stop)
        write_header "Stopping Stacks"
        
        sorted_stacks=$(sort_stacks_reverse $stacks_to_process)
        while read -r stack; do
            stop_stack "$stack"
            sleep 2
        done <<< "$sorted_stacks"
        
        write_status "All requested stacks stopped" "success"
        ;;
    
    restart)
        write_header "Restarting Stacks"
        
        # Stop
        sorted_stacks=$(sort_stacks_reverse $stacks_to_process)
        while read -r stack; do
            stop_stack "$stack"
        done <<< "$sorted_stacks"
        
        sleep 5
        
        # Start
        sorted_stacks=$(sort_stacks_forward $stacks_to_process)
        while read -r stack; do
            start_stack "$stack"
            sleep 5
        done <<< "$sorted_stacks"
        
        write_status "All requested stacks restarted" "success"
        ;;
    
    status)
        write_header "Stack Status"
        
        while read -r stack; do
            get_stack_status "$stack"
        done <<< "$stacks_to_process"
        ;;
    
    health)
        write_header "Stack Health Check"
        
        while read -r stack; do
            get_stack_health "$stack"
            echo ""
        done <<< "$stacks_to_process"
        ;;
    
    logs)
        while read -r stack; do
            show_stack_logs "$stack"
        done <<< "$stacks_to_process"
        ;;
    
    validate)
        validate_deployment
        ;;
    
    cleanup)
        cleanup_stack
        ;;
    
    *)
        write_status "Unknown action: $ACTION" "error"
        exit 1
        ;;
esac

echo ""
write_status "Operation completed" "success"
