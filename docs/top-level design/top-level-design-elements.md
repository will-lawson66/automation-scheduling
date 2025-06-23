# Scheduler Application 

## Top-level application components
- CMR
    - loads CMR file rows into C# classes
    - has internal model based on file information
- Data persistence
    - domain-like entity storage, robust service layer
    - can be extended to multiple databases with the same pattern
- Comms
    - gRPC Gateway performant, observable communication with other applications and services
        - will be extended to absorb high-volume event streams and observability
- Workflow management
    - Process manager pattern for stateful orchestration of workflows across services
    - Stateless state machine for rich context keeping
    - these two patterns can be integrated across the scheduler
- Application Services
    - Assay manager service
        - will create, maintain, process, coordinate and dispose AssaySamples and their associated data structures.
        - may need to scale horizontally
    - Assay execution planner service
        - will service collections of AssaySamples in order to create a unified assay execution plan
        - need to clarify if assay execution plan is sequence group-based or HAL-based, or scientific
        - will process AssaySamples via rules engine also to compute QC, CC, Cal, blank replicate generation
            - these will also result in AssaySamples? need to confirm
    - SequenceGroup manager service
        - tasked with managing a collection of sequence groups associated with assay samples
        - probably will be interested in system operational sequences also
        - create and maintain hardware execution plan
    - Hardware execution planner service
        - TBD - 
    - Inventory service
        - is called for any assay in order to ascertain consumables ("articles") needed for all assays and other oprations
- Other features
    - Event management
        - rich event-driven-architectural components to consume hardware events and issue scheduling events
    - FLR context
        - the FLR is the aggregate result of an assay run/CMR
        - there will be a stateful component to manage the FLR context for all assays

