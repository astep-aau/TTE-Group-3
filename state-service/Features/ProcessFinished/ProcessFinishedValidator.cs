using FluentValidation;

namespace StateService.Features.ProcessFinished
{
    public class ProcessFinishedMessageValidator : AbstractValidator<ProcessFinishedMessage>
    {
        public ProcessFinishedMessageValidator()
        {
            RuleFor(x => x.Pid).GreaterThan(0);
            RuleFor(x => x.ResultSummary).NotEmpty().MaximumLength(1024);
            RuleFor(x => x.CorrelationId).MaximumLength(64);
        }
    }
}
